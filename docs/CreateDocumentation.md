```markdown
# Documentación Técnica

## Nombre del archivo: CreateDocumentation.cs

## Descripción funcional

El archivo `CreateDocumentation.cs` es una Azure Function que se utiliza para analizar archivos de un repositorio de GitHub, generar documentación en formato Markdown para dicho archivo y subir o actualizar la documentación en el repositorio. Además, esta función está diseñada para trabajar con integraciones de GitHub y OpenAI GPT para facilitar la generación de documentación técnica. También admite la gestión de documentos fragmentados cuando el tamaño del archivo es demasiado grande.

---

## Descripción técnica

### Estructura general

El archivo contiene las siguientes propiedades, métodos y funciones definidas por el usuario:

#### Propiedades
1. **`_httpClient`**
   - **Tipo:** `HttpClient` (C#).
   - **Descripción:** Instancia de `HttpClient` utilizada para realizar solicitudes HTTP a la API de GitHub y Azure OpenAI.
   - **Modificada por:** Ninguna función la modifica directamente, pero se utiliza como medio para enviar solicitudes GET y POST.

2. **`_logger`**
   - **Tipo:** `ILogger` (C#).
   - **Descripción:** Instancia del log para registrar información sobre los procesos de la función. Se instancia mediante una fábrica de loggers en el método constructor.
   - **Modificada por:** Ninguna función la modifica directamente, pero se utiliza para registrar logs.

3. **`MaxFragmentLength`**
   - **Tipo:** `const int` (C#).
   - **Descripción:** Define el tamaño máximo de un fragmento de texto para la división de archivos grandes, utilizado en `GenerateDocumentationWithFragments`.

---

### Métodos y Funciones

1. **`CreateDocumentation` (Constructor)**
   - **Lenguaje:** C#.
   - **Parámetros:**
     - `loggerFactory`: Instancia de la fábrica de loggers de tipo `ILoggerFactory`.
   - **Variables modificadas:** 
     - Inicializa las propiedades `_httpClient` y `_logger`.
   - **Condiciones o validaciones:** No aplica.
   - **Descripción:** Este es el constructor de la clase `CreateDocumentation`. Crea la instancia del cliente HTTP para las comunicaciones de red y establece un logger para registrar mensajes sobre el flujo de ejecución.
   - **Valor de retorno:** No tiene retorno explícito.

---

2. **`Run`**
   - **Lenguaje:** C#.
   - **Parámetros:**
     - `[HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req`: Representa el objeto de la petición HTTP recibida por la Azure Function.
   - **Variables modificadas:** Ninguna.
   - **Condiciones o validaciones:**
     - Validación de respuesta exitosa (`IsSuccessStatusCode`) en distintos puntos para el manejo de errores de las APIs utilizadas.
   - **Descripción:** Este es el método principal de la clase, que se ejecuta cuando la Azure Function es llamada. Realiza las siguientes tareas:
     1. Extrae el cuerpo y propiedades del JSON recibido (`repo` y `branch`) para determinar el repositorio y la rama que se van a analizar.
     2. Configura las cabeceras de autorización de `HttpClient` utilizando tokens de acceso obtenidos de las variables de entorno: `GITHUB_TOKEN` y `OPENAI_API_KEY`.
     3. Obtiene la lista de archivos del repositorio especificado mediante la API de GitHub y filtra aquellos que sean archivos fuente de extensión `.cs`, `.js`, `.html` y excluye directorios no deseados (`bin/`, `obj/`, etc.).
     4. Itera sobre los archivos obtenidos para realizar las siguientes tareas:
         - Recupera el contenido del archivo codificado en base64 desde GitHub.
         - Compara el contenido con los documentos existentes en el repositorio para determinar si es necesario realizar un nuevo análisis.
         - En caso de que el archivo sea demasiado grande, lo fragmenta utilizando el método `GenerateDocumentationWithFragments`.
         - Subir o actualizar el archivo de documentación en formato `.md` en el directorio del repositorio, añadiendo el SHA del archivo para realizar la verificación de cambios futuros.
     5. Devuelve un mensaje indicando si el análisis fue completado correctamente.
   - **Valor de retorno:** Devuelve una respuesta HTTP (`HttpResponseData`) con un mensaje, en este caso `"✅ Análisis completado."`.

---

3. **`GenerateDocumentationWithFragments`**
   - **Lenguaje:** C#.
   - **Parámetros:**
     - `filePath`: Ruta del archivo analizado.
     - `content`: Contenido del archivo como una cadena de texto.
     - `apiKey`: Clave de acceso para la API de Azure OpenAI GPT.
   - **Variables modificadas:**
     - `sb` (StringBuilder): Se acumulan los fragmentos de Markdown generados.
   - **Condiciones o validaciones:**
     - Divide el contenido total en fragmentos cuyo tamaño sea menor o igual a `MaxFragmentLength`.
   - **Descripción:** Este método divide el contenido del archivo grande en fragmentos si supera el máximo determinado por `MaxFragmentLength`. Cada fragmento se usa como entrada para construir un prompt que genera documentación técnica en Markdown utilizando el método `GenerateDocumentationAsync`. Luego une los fragmentos generados en un único string.
   - **Valor de retorno:** Devuelve un string que contiene la documentación completa, incluida cualquier fragmentación procesada.

---

4. **`BuildPrompt`**
   - **Lenguaje:** C#.
   - **Parámetros:**
     - `filePath`: Ruta del archivo analizado.
     - `codeFragment`: Fragmento específico de código.
     - `fragmentIndex`: Índice del fragmento actual.
     - `totalFragments`: Total de fragmentos en el archivo.
   - **Variables modificadas:** Ninguna.
   - **Condiciones o validaciones:** Ninguna.
   - **Descripción:** Construye un prompt detallado que incluye las instrucciones de cómo documentar un fragmento de código. Incluye el fragmento de código, índice del fragmento y total de fragmentos junto con las normas para la generación del Markdown.
   - **Valor de retorno:** Devuelve un string que contiene el prompt para la generación de documentación técnica en formato Markdown.

---

5. **`GenerateDocumentationAsync`**
   - **Lenguaje:** C#.
   - **Parámetros:**
     - `prompt`: Prompt de texto que será enviado al modelo OpenAI GPT.
     - `apiKey`: Clave de acceso para la API de Azure OpenAI GPT.
   - **Variables modificadas:** Ninguna.
   - **Condiciones o validaciones:**
     - La respuesta HTTP recibida debe ser exitosa (`response.EnsureSuccessStatusCode()`).
   - **Descripción:** Este método utiliza la API de Azure OpenAI GPT para generar documentación técnica en formato Markdown a partir del prompt proporcionado. Se realiza una solicitud POST a la API con el prompt definido y se devuelve como resultado la respuesta generada por el modelo.
   - **Valor de retorno:** Devuelve un string que contiene la documentación generada en formato Markdown.

---

### Consideraciones

- Este archivo depende de varias bibliotecas externas, como `Microsoft.AspNetCore.Mvc`, `Microsoft.Azure.Functions.Worker`, `System.Text.Json`, entre otras.
- La función `Run` es el punto de entrada de la Azure Function, mientras que las funciones auxiliares (`GenerateDocumentationWithFragments`, `BuildPrompt`, `GenerateDocumentationAsync`) ayudan en el proceso de análisis del contenido y generación de documentación.
- El sistema gestiona verificación de cambios en los archivos a través de los valores de SHA, lo cual evita redundancias en la generación de documentación.
- Es importante que las variables de entorno `GITHUB_TOKEN` y `OPENAI_API_KEY` estén correctamente configuradas para el funcionamiento de este archivo, ya que son esenciales para la interacción con los servicios externos.

---

## Fragmento 1 de 1

Esta función proporciona una solución automatizada robusta para generar documentación técnica de archivos en repositorios GitHub utilizando tecnologías modernas como Azure Functions, OpenAI GPT y servicios web. Puede ser útil para equipos de desarrollo que busquen mejorar la calidad y mantenibilidad de su código manteniendo documentación actualizada.


SHA:d0a65a24da096e405be691ecf6a0c520cd08528b