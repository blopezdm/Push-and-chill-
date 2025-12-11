using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using System.Net;
using static System.Net.WebRequestMethods;
using System.Security.Cryptography;

namespace FunctionDocs
{
    public class CreateDocumentation_Azure
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private const int MaxFragmentLength = 15000;

        public CreateDocumentation_Azure(ILoggerFactory loggerFactory)
        {
            _httpClient = new HttpClient();
            _logger = loggerFactory.CreateLogger<CreateDocumentation_Azure>();
        }

        [Function("CreateDocumentation_Azure")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var jsonDoc = JsonDocument.Parse(requestBody);

            string org = jsonDoc.RootElement.GetProperty("Org").GetString();
            string project = jsonDoc.RootElement.GetProperty("Project").GetString();
            string repoId = jsonDoc.RootElement.GetProperty("RepoId").GetString();
            string branch = jsonDoc.RootElement.GetProperty("Branch").GetString();
            string pat = Environment.GetEnvironmentVariable("AZURE_TOKEN");
            string openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            string openurl = Environment.GetEnvironmentVariable("OPENAI_URL");
            string model = "gpt-4o";

            _logger.LogInformation($"Iniciando análisis para repo: {org}/{project}, repoId: {repoId}, branch: {branch}");
            // 1. Listar archivos
            var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authToken);

            // string url = $"https://dev.azure.com/{org}/{project}/_apis/git/repositories/{repoId}/items?recursionLevel=Full&versionDescriptor.version={branch}&api-version=7.1-preview.1";
            string url = $"https://dev.azure.com/{org}/{project}/_apis/git/repositories/{repoId}/items?scopePath=/&recursionLevel=Full&versionDescriptor.version={branch}&api-version=7.1-preview.1";
            var responsedev = await _httpClient.GetAsync(url);
            responsedev.EnsureSuccessStatusCode();

            var json = await responsedev.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);


            var files = doc.RootElement.GetProperty("value")
            .EnumerateArray()
            .Where(e =>
            {
                // Validar si es archivo
                if (e.TryGetProperty("isFolder", out var isFolderProp))
                    return !isFolderProp.GetBoolean(); // solo archivos
                // fallback usando gitObjectType
                if (e.TryGetProperty("gitObjectType", out var typeProp))
                    return typeProp.GetString() == "blob";
                return false;
            })
            .Select(e => new
            {
                Path = e.GetProperty("path").GetString(),
                Sha = e.TryGetProperty("objectId", out var shaProp) ? shaProp.GetString() : ""
            })
            .Where(f =>
                (f.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                 f.Path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
                 f.Path.EndsWith(".html", StringComparison.OrdinalIgnoreCase)) &&
                !f.Path.Contains("/bin/", StringComparison.OrdinalIgnoreCase) &&
                !f.Path.Contains("/obj/", StringComparison.OrdinalIgnoreCase) &&
                !f.Path.Contains("/.vs/", StringComparison.OrdinalIgnoreCase) &&
                !f.Path.Contains("/Debug/", StringComparison.OrdinalIgnoreCase) &&
                !f.Path.Contains("/Release/", StringComparison.OrdinalIgnoreCase))
            .ToList();

            foreach (var file in files)
            {
                _logger.LogInformation($"Procesando archivo: {file}");

                // 2. Obtener contenido
                var content = await GetFileContentAsync(org, project, repoId, branch, file.Path, pat);

                // 3. Calcular SHA
                string fileSha = GetSha(content);

                // 4. Verificar si ya existe doc con mismo SHA
                string mdPath = $"docs/{Path.ChangeExtension(file.Path, ".md")}";
                string existingMd = await TryGetFileContentAsync(org, project, repoId, branch, mdPath, pat);

                if (!string.IsNullOrEmpty(existingMd) && existingMd.Contains($"SHA:{fileSha}"))
                {
                    _logger.LogInformation($"Sin cambios: {file}, se omite el análisis.");
                    continue;
                }

                // 5. Analizar con OpenAI fragmentando
                string fullDoc = await AnalyzeFileInFragments(content, file.Path, openAiKey, model);

                // 6. Añadir SHA al final del documento
                string docWithSha = fullDoc + $"\n\nSHA:{fileSha}";

                // 7. Commit en DevOps
                await CommitFileAsync(org, project, repoId, branch, mdPath, docWithSha, pat);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("✅ Análisis completado y docs generados.");
            return response;
        }

        private async Task<string> AnalyzeFileInFragments(string content, string filePath, string key, string model)
        {
            var sb = new StringBuilder();
            int start = 0;
            int fragmentIndex = 1;
            int totalFragments = (int)Math.Ceiling((double)content.Length / MaxFragmentLength);

            while (start < content.Length)
            {
                int length = Math.Min(MaxFragmentLength, content.Length - start);
                string fragment = content.Substring(start, length);

                string prompt = BuildPrompt(filePath, fragment, fragmentIndex, totalFragments);

                string mdFragment = await AnalyzeWithOpenAI(prompt, key, model);

                sb.AppendLine(mdFragment);
                start += length;
                fragmentIndex++;
            }

            return sb.ToString();
        }

        private string BuildPrompt(string filePath, string codeFragment, int fragmentIndex, int totalFragments)
        {
            string normas = @"
            1. **Nombre del archivo:** indica el nombre del archivo analizado.
            2. **Descripción funcional:** explica de manera clara y concisa qué hace el archivo en general.
            3. **Descripción técnica:** detalla la implementación técnica, incluyendo:
               - Una lista de **todos los métodos y funciones definidos por el usuario** (ignora funciones/métodos de librerías externas o del sistema).
               - Para cada función/método, indica:
                 - Nombre de la función/método.
                 - Lenguaje (C#, JS, etc.).
                 - Parámetros (nombre y breve descripción si se puede inferir).
                 - Variables modificadas (propiedades o variables internas/globales afectadas).
                 - Condiciones, validaciones o requisitos dentro de la función.
                 - Descripción detallada de lo que hace la función, usando nombres lógicos de variables y parámetros.
                 - Valor de retorno (si aplica).
               - Mantén los nombres de variables y funciones tal como aparecen en el código.
               - Si hay funciones anidadas, documenta cada una por separado. 
            4. Sé preciso y no generes explicaciones genéricas.
            5. Si el fragmento es parte de un archivo más grande, indica que es un fragmento y su posición (por ejemplo, 'Fragmento 1 de 3').
            6. Sólo el primer fragmento tendrá el nombre del archivo y la descripción funcional general no hagas etiquetas Markdown de bloque. Los fragmentos posteriores solo documenta las funciones técnicas, no repitas nombre de archivo ni descripción funcional, ni etiquetas Markdown de bloque.";

            return $"Tu tarea es analizar el siguiente archivo y generar **documentación técnica completa en Markdown**.\n\nFragmento {fragmentIndex} de {totalFragments}\n\nInstrucciones:\n{normas}\n\nArchivo: {filePath}\n\nCódigo:\n{codeFragment}";
        }

        private async Task<string> AnalyzeWithOpenAI(string prompt, string key, string model,string endpoint)
        {
            
            string deployment = "gpt-4.1";
            string apiVersion = "2025-04-14";

            var payload = new
            {
                messages = new[]
                {
                new { role = "system", content = "Eres un asistente experto en programación y documentación técnica." },
                new { role = "user", content = prompt }
            },
                temperature = 1,
                top_p = 1,
                max_tokens = 16000
            };

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"{endpoint}/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}", content);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);
            var result = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            return result!;
        }

        private string GetSha(string content)
        {
            using var sha = SHA1.Create();
            var bytes = Encoding.UTF8.GetBytes(content);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }

        private async Task<string> TryGetFileContentAsync(string org, string project, string repoId, string branch, string path, string pat)
        {
            try { return await GetFileContentAsync(org, project, repoId, branch, path, pat); }
            catch { return null; }
        }

       

        private async Task<string> GetFileContentAsync(string org, string project, string repoId, string branch, string path, string pat)
        {
            var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authToken);

            string url = $"https://dev.azure.com/{org}/{project}/_apis/git/repositories/{repoId}/items?path={Uri.EscapeDataString(path)}&versionDescriptor.version={branch}&includeContent=true&api-version=7.1-preview.1";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        private async Task CommitFileAsync(string org, string project, string repoId, string branch, string filePath, string content, string pat)
        {
            var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authToken);

            string lastCommitId = await GetLastCommitIdAsync(org, project, repoId, branch, pat);

            var body = new
            {
                refUpdates = new[]
                {
                new { name = $"refs/heads/{branch}", oldObjectId = lastCommitId }
            },
                commits = new[]
                {
                new {
                    comment = $"docs: análisis automático de {filePath}",
                    changes = new[]
                    {
                        new {
                            changeType = "add",
                            item = new { path = filePath },
                            newContent = new { content = content, contentType = "rawtext" }
                        }
                    }
                }
            }
            };

            string url = $"https://dev.azure.com/{org}/{project}/_apis/git/repositories/{repoId}/pushes?api-version=7.1-preview.2";
            var response = await _httpClient.PostAsync(url, new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
        }

        private async Task<string> GetLastCommitIdAsync(string org, string project, string repoId, string branch, string pat)
        {
            var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authToken);

            string url = $"https://dev.azure.com/{org}/{project}/_apis/git/repositories/{repoId}/refs?filter=heads/{branch}&api-version=7.1-preview.1";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            return doc.RootElement.GetProperty("value")[0].GetProperty("objectId").GetString();
        }

        public record RequestDto(string Org, string Project, string RepoId, string Branch);
    
}
}
