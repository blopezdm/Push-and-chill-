# CreateDocumentation.cs

## Descripción funcional:
El archivo **`CreateDocumentation.cs`** define una función de Azure que genera documentación técnica en formato Markdown a partir del contenido de archivos en un repositorio de GitHub. Este proceso incluye obtener los archivos del repositorio, analizar su contenido, dividirlos en fragmentos si son muy grandes, generar documentación para cada fragmento utilizando un modelo OpenAI GPT-4, y actualizar o crear dichos archivos de documentación en el repositorio. Además, se realiza un registro detallado del progreso de la operación.

---

## Descripción técnica:

### **Clase `CreateDocumentation`**
Esta clase contiene toda la lógica de la función para generar documentación técnica con Azure Functions.

---

### **Métodos definidos**

---

#### **Constructor: `CreateDocumentation(ILoggerFactory loggerFactory)`**
**Lenguaje:** C#

- **Parámetros:**
  - `ILoggerFactory loggerFactory`: Instancia del servicio para crear loggers.

- **Variables modificadas:**
  - `_httpClient`: Inicializa un objeto `HttpClient` para realizar llamadas HTTP.
  - `_logger`: Se configura como un logger utilizando el servicio `ILoggerFactory`.

- **Descripción:** 
  Inicializa un cliente HTTP (`_httpClient`) para realizar solicitudes y un logger (`_logger`) para registrar eventos y acciones realizadas por la clase.

- **Valor de retorno:**
  - No retorna ningún valor.

---

#### **Método: `Run(HttpRequestData req)`**
**Lenguaje:** C#

- **Parámetros:**
  - `HttpRequestData req`: Representa una solicitud HTTP entrante desde `HttpTrigger`, conteniendo datos como el cuerpo y encabezados de la solicitud.

- **Variables modificadas:**
  - `_httpClient`: Las cabeceras de autorización y agente de usuario del cliente HTTP se configuran para realizar solicitudes a la API de GitHub.
  - Valores obtenidos desde variables de entorno: `GITHUB_TOKEN`, `OPENAI_API_KEY`.

- **Condiciones, validaciones o requisitos:**
  - La función espera que se incluya un cuerpo JSON con las propiedades `repo` y `branch`.
  - Verifica si los archivos en el repositorio cumplen con ciertas condiciones, como que sean archivos con extensiones específicas (`.cs`, `.js`, `.html`) y que no correspondan a ciertas rutas como `/bin/`, `/obj/`, etc.
  - Valida si ya existe documentación basada en el hash (`SHA`) del archivo; si no hay cambios, omite la actualización de la documentación.

- **Descripción detallada:**
  Este método implementa la función principal `CreateDocumentation` y sigue estos pasos:
  1. Lee el cuerpo de la solicitud que debe contener los datos del repositorio (`repo`) y branch (`branch`) de GitHub.
  2. Recupera token de acceso para GitHub y clave API para OpenAI desde las variables de entorno.
  3. Configura el cliente HTTP con el token de autorización y el agente de usuario requerido por la API de GitHub.
  4. Obtiene una lista de archivos del repositorio mediante la API de GitHub. Filtra los archivos para excluir:
     - Archivos con extensiones no deseadas.
     - Archivos ubicados en rutas especificadas como `/bin/`, `/obj/`.
     - Otros archivos irrelevantes, como `AssemblyInfo.cs`.
  5. Itera sobre cada archivo seleccionado y obtiene su contenido desde GitHub mediante `GetAsync`.
  6. Si el archivo ya tiene un Markdown actualizado con la misma `SHA`, se salta su procesamiento.
  7. Verifica si el archivo debe dividirse en fragmentos (basado en su longitud) y genera documentación técnica completa utilizando el método `GenerateDocumentationWithFragments()`.
  8. Sube o actualiza la documentación Markdown generada en el repositorio de GitHub a través de una solicitud `PUT`.

- **Valor de retorno:**
  - Devuelve un objeto `HttpResponseData` con el estado de la operación. Incluye un mensaje indicando el estado de completado (`✅ Análisis completado.`).

---

#### **Método: `GenerateDocumentationWithFragments(string filePath, string content, string apiKey)`**
**Lenguaje:** C#

- **Parámetros:**
  - `string filePath`: Ruta del archivo que será analizado.
  - `string content`: Contenido del archivo fuente a documentar.
  - `string apiKey`: Clave API para comunicarse con el servicio GPT de OpenAI.

- **Variables modificadas:**
  - `sb`: Construye un objeto `StringBuilder` para concatenar la documentación generada.
  - `start`: Inicializa y actualiza el índice inicial del fragmento actual.
  - `fragmentIndex`: Incrementa el índice del fragmento actual durante la iteración.

- **Condiciones, validaciones o requisitos:**
  - La función divide el contenido en fragmentos de tamaño máximo definido por la constante `MaxFragmentLength`. 
  - Calcula el número total de fragmentos antes de iniciar el proceso de documentación.

- **Descripción detallada:**
  Este método divide contenidos grandes en fragmentos más pequeños y genera documentación en Markdown para cada uno. Por cada fragmento:
  1. Calcula el tamaño del fragmento en función de la constante `MaxFragmentLength`.
  2. Genera un prompt específico para este fragmento utilizando el método auxiliar `BuildPrompt`.
  3. Llama al método `GenerateDocumentationAsync` para obtener la documentación generada por el modelo GPT.
  4. Concadena el fragmento generado al objeto `StringBuilder`.

  Al final, la función devuelve la documentación completa como un único string en formato Markdown.

- **Valor de retorno:**
  - Retorna un string en formato Markdown con toda la documentación técnica generada para el archivo.

---

#### **Método: `BuildPrompt(string filePath, string codeFragment, int fragmentIndex, int totalFragments)`**
**Lenguaje:** C#

- **Parámetros:**
  - `string filePath`: Ruta del archivo que se está analizando.
  - `string codeFragment`: Fragmento del contenido del archivo que será analizado.
  - `int fragmentIndex`: Índice actual del fragmento a procesar.
  - `int totalFragments`: Número total de fragmentos en los que se ha dividido el archivo.

- **Variables modificadas:** Ninguna.

- **Condiciones, validaciones o requisitos:**
  - No hay validaciones específicas en este método, pero se asegura de incluir las instrucciones proporcionadas en el prompt.

- **Descripción detallada:**
  Construye el texto del prompt para enviar al servicio GPT. El prompt incluye:
  - Las instrucciones para crear la documentación.
  - Nombre y ruta del archivo actualmente en proceso.
  - Un fragmento de código del archivo.
  - Información del índice y cantidad total de fragmentos.

- **Valor de retorno:**
  - Retorna un string que representa el prompt a enviar para la generación de documentación.

---

#### **Método: `GenerateDocumentationAsync(string prompt, string apiKey)`**
**Lenguaje:** C#

- **Parámetros:**
  - `string prompt`: Prompt creado que contiene las instrucciones y el fragmento de código a documentar.
  - `string apiKey`: Clave API para autenticar la solicitud al servicio OpenAI GPT-4o.

- **Variables modificadas:** Ninguna.

- **Condiciones, validaciones o requisitos:**
  - La función valida el éxito de la solicitud HTTP con `EnsureSuccessStatusCode()`.

- **Descripción detallada:**
  Este método envía un prompt al servicio OpenAI GPT-4o en Azure para generar la documentación técnica en formato Markdown. Los pasos son:
  1. Configura las cabeceras de autorización usando la clave API proporcionada.
  2. Construye el payload necesario con el prompt y otros parámetros de generación (temperatura, longitud máxima permitida, etc.).
  3. Realiza una solicitud POST al servicio OpenAI, especificando el endpoint, deployment y versión de la API.
  4. Procesa la respuesta, extrayendo el contenido generado en formato JSON.

- **Valor de retorno:**
  - Retorna el texto generado en formato Markdown.

---

### **Constantes**
1. `private const int MaxFragmentLength = 15000`:
   - Define el máximo número de caracteres permitido para dividir un archivo en fragmentos.

---

## Otros aspectos técnicos relevantes:
- **Integración con APIs:**
  - Se utiliza la API de GitHub para interactuar con repositorios, acceder a archivos y subir documentación generada.
  - Se utiliza Azure OpenAI (GPT-4o) para generar la documentación en formato Markdown.

- **Librerías externas utilizadas:**
  - `System.Net.Http`: Para realizar solicitudes HTTP.
  - `System.Text.Json`: Para manipular JSON de las respuestas de APIs.
  - `Microsoft.AspNetCore.DataProtection.KeyManagement`: Para el manejo de llaves.
  - `Microsoft.Extensions.Logging`: Para manejo de registros (`Logs`).
  - `Microsoft.Azure.Functions.Worker`: Para definir la función como una Azure Function.
  
---

### Estructura del flujo general (resumen):
1. Configuración inicial (HttpClient y Logger).
2. Obtención de datos de un repositorio en GitHub.
3. Filtrado de archivos relevantes.
4. Recuperación de contenido de cada archivo.
5. Generación de documentación en fragmentos para archivos grandes.
6. Subida o actualización de la documentación en GitHub.

---

### Fragmentación del Archivo
Si un archivo excede el tamaño especificado (15,000 caracteres), se divide en varios fragmentos para generar documentación por cada parte del contenido. Esto asegura que se pueda manejar archivos extensos sin superar los límites de procesamiento del modelo GPT.

---

## Notas:
El archivo analizado es el **Fragmento 1 Total Fragmento: 1**, lo que implica que este es el archivo completo. Sin embargo, si en futuros desarrollos se incluye la capacidad de analizar más fragmentos, cada uno de ellos deberá ser documentado siguiendo la misma estructura definida.


SHA:134d35024a506c756835483029b22203fa76bc0f