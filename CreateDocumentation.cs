using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace FunctionDocs
{
    public class CreateDocumentation
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private const int MaxFragmentLength = 15000;

        public CreateDocumentation(ILoggerFactory loggerFactory)
        {
            _httpClient = new HttpClient();
            _logger = loggerFactory.CreateLogger<CreateDocumentation>();
        }

        [Function("CreateDocumentation")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            // Leer body JSON
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var jsonDoc = JsonDocument.Parse(requestBody);

            string repo = jsonDoc.RootElement.GetProperty("repo").GetString();
            string branch = jsonDoc.RootElement.GetProperty("branch").GetString();

            string githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            string openaiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        
            string outDir = "docs";

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("token", githubToken);
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AzureFunctionApp");

            // 1. Listar archivos del repo
            var filesResponse = await _httpClient.GetStringAsync(
                $"https://api.github.com/repos/{repo}/git/trees/{branch}?recursive=1");

            _logger.LogInformation($"https://api.github.com/repos/{repo}/git/trees/{branch}?recursive=1");

            using var doc = JsonDocument.Parse(filesResponse);
            var files = doc.RootElement.GetProperty("tree")
                 .EnumerateArray()
                 .Where(e => e.GetProperty("type").GetString() == "blob") // solo archivos
                 .Select(e => new
                 {
                     Path = e.GetProperty("path").GetString(),
                     Sha = e.GetProperty("sha").GetString()
                 })
                 .Where(f =>
                     (f.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                      f.Path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
                      f.Path.EndsWith(".html", StringComparison.OrdinalIgnoreCase)) &&
                     !f.Path.Contains("/bin/") &&
                     !f.Path.Contains("/obj/") &&
                     !f.Path.Contains("/.git/") &&
                     !f.Path.Contains("/.vs/") &&
                     !f.Path.EndsWith("AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase) // opcional
                 )
                 .ToList();

            _logger.LogInformation($"Cantidad de archivos: {files.Count}");

            foreach (var file in files)
            {
                // 2️⃣ Obtener contenido del archivo desde GitHub
                var contentResponse = await _httpClient.GetAsync(
                    $"https://api.github.com/repos/{repo}/contents/{file.Path}?ref={branch}");
                contentResponse.EnsureSuccessStatusCode();
                var contentJson = JsonDocument.Parse(await contentResponse.Content.ReadAsStringAsync());
                string fileContentBase64 = contentJson.RootElement.GetProperty("content").GetString()!;
                string fileContent = Encoding.UTF8.GetString(Convert.FromBase64String(fileContentBase64.Replace("\n", "")));

                // 3️⃣ Ruta dentro del repo para MD
                string githubPath = $"docs/{Path.ChangeExtension(file.Path, ".md")}";

                // 4️⃣ Verificar si ya existe el MD con SHA
                bool skip = false;
                var existingResponse = await _httpClient.GetAsync(
                    $"https://api.github.com/repos/{repo}/contents/{githubPath}?ref={branch}");
                string existingSha = null!;
                if (existingResponse.IsSuccessStatusCode)
                {
                    var existingJson = JsonDocument.Parse(await existingResponse.Content.ReadAsStringAsync());
                    string existingContentBase64 = existingJson.RootElement.GetProperty("content").GetString()!;
                    string existingContent = Encoding.UTF8.GetString(Convert.FromBase64String(existingContentBase64.Replace("\n", "")));

                    if (existingContent.Contains($"SHA:{file.Sha}"))
                    {
                        _logger.LogInformation($"Sin cambios: {file.Path}, se omite.");
                        skip = true;
                    }
                    else
                    {
                        existingSha = existingJson.RootElement.GetProperty("sha").GetString()!;
                    }
                }

                if (skip) continue;

                // 5️⃣ Fragmentar archivo si es muy grande y generar Markdown
                string fullMarkdown = await GenerateDocumentationWithFragments(file.Path, fileContent, openaiKey);

                // 6️⃣ Subir o actualizar archivo en GitHub
                var payload = new
                {
                    message = $"Actualizar análisis de {file.Path}",
                    content = Convert.ToBase64String(Encoding.UTF8.GetBytes(fullMarkdown + $"\n\nSHA:{file.Sha}")),
                    branch = branch,
                    sha = string.IsNullOrEmpty(existingSha) ? null : existingSha
                };

                var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
                var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var uploadResponse = await _httpClient.PutAsync(
                    $"https://api.github.com/repos/{repo}/contents/{githubPath}", httpContent);
                uploadResponse.EnsureSuccessStatusCode();

                _logger.LogInformation($"Documento actualizado/subido: {githubPath}");
            }

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await response.WriteStringAsync("✅ Análisis completado.");
            return response;
        }

        // Divide archivo grande en fragmentos y genera Markdown completo
        private static async Task<string> GenerateDocumentationWithFragments(string filePath, string content, string apiKey)
        {
            var sb = new StringBuilder();
            int start = 0;
            int fragmentIndex = 1;
            int totalFragments = (int)Math.Ceiling((double)content.Length / MaxFragmentLength);

            while (start < content.Length)
            {
                int length = Math.Min(MaxFragmentLength, content.Length - start);
                string fragment = content.Substring(start, length);

                string prompt = BuildPrompt(filePath, fragment, fragmentIndex,totalFragments);
                string mdFragment = await GenerateDocumentationAsync(prompt, apiKey);

                sb.AppendLine(mdFragment);
                start += length;
                fragmentIndex++;
            }

            return sb.ToString();
        }

        // Construye prompt con normas y fragmento de código
        private static string BuildPrompt(string filePath, string codeFragment, int fragmentIndex, int totalFragments)
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

            return 
                $"Tu tarea es analizar el siguiente archivo y realizar un **documento técnico completo** en Markdown listo para README.md.\n\nFragmento {fragmentIndex} Total Fragmento: {totalFragments}\n\nInstrucciones:\n{normas}\n\nArchivo: {filePath}\n\nCódigo:\n{codeFragment}";
        }

        // Llama a Azure OpenAI GPT-4o
        private static async Task<string> GenerateDocumentationAsync(string prompt, string apiKey)
        {
            string endpoint = "https://openai-netcore.openai.azure.com/";
            string deployment = "gpt-4o";
            string apiVersion = "2024-04-01-preview";

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
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"{endpoint}/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}", content);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);
            var result = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            return result!;
        }
    }
}
