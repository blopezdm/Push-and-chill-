```markdown
# CreateDocumentation_Azure.cs

## Descripción funcional
Este archivo es una clase implementada en C# que contiene la definición de una función de Azure. Su funcionalidad principal es analizar archivos de un repositorio Git alojado en Azure DevOps, generar documentación técnica de esos archivos utilizando una API de OpenAI y almacenar la documentación generada nuevamente en el repositorio. La clase incluye la lógica para la lectura y validación de los archivos en el repositorio, el análisis del contenido de los archivos en fragmentos y la gestión de autenticación y solicitudes hacia los servicios externos (Azure DevOps, OpenAI).

---

## Descripción técnica

El archivo define varias funciones y métodos. A continuación, se detalla cada uno con sus respectivos parámetros, operaciones y retornos.

### **Clase `CreateDocumentation_Azure`**

**Lenguaje:** C#

La clase principal que implementa una función de Azure y contiene múltiples métodos para el análisis y generación de documentación técnica. Se utilizan librerías de .NET y extensiones para facilitar el procesamiento de HTTP requests/responses, comunicación con APIs externas y la serialización/deserialización de datos JSON.

### **Constructor**

#### **Método: `CreateDocumentation_Azure(ILoggerFactory loggerFactory)`**

**Lenguaje:** C#

**Parámetros:**
- `ILoggerFactory loggerFactory`: Instancia para crear un logger usado para registrar eventos en el sistema.

**Variables modificadas:**
- `_httpClient`: Inicializa el cliente para realizar solicitudes HTTP externas.
- `_logger`: Instancia de logger para registrar mensajes relacionados con la ejecución de la clase.

**Descripción:**  
Este constructor inicializa los objetos necesarios para realizar solicitudes HTTP y registrar mensajes de log durante la ejecución del programa.

---

### **Método: `Run(HttpRequestData req)`**

**Lenguaje:** C#

**Parámetros**:
- `HttpRequestData req`: Representa el objeto de solicitud HTTP entrante, que contiene información como el cuerpo de la solicitud y los encabezados.

**Variables modificadas**:
- `org` (string): Organización de Azure DevOps extraída del cuerpo de la solicitud (JSON).
- `project` (string): Proyecto en Azure DevOps extraído del cuerpo de la solicitud (JSON).
- `repoId` (string): Identificador del repositorio en Azure DevOps extraído del cuerpo de la solicitud (JSON).
- `branch` (string): Nombre de la rama del repositorio extraída del cuerpo de la solicitud (JSON).
- `pat` (string): Token de autenticación de Azure DevOps obtenido de las variables de entorno.
- `openAiKey` (string): Clave API de OpenAI obtenida de las variables de entorno.
- `_httpClient.DefaultRequestHeaders.Authorization`: Configurada para añadir un token de acceso básico para autenticar solicitudes HTTP.

**Condiciones, validaciones o requisitos**:
- Validación de datos obtenidos desde el cuerpo de la solicitud (`Org`, `Project`, `RepoId`, `Branch`).
- Verificación de archivos en el repositorio para descartar carpetas y elegir únicamente los archivos con extensiones `.cs`, `.js`, y `.html`, excluyendo archivos en ciertos directorios como `bin/`, `obj/`, `.vs/`.

**Descripción:**  
Este método define la función principal para la ejecución en Azure. Realiza los siguientes pasos:
1. **Leer y parsear el cuerpo de la solicitud HTTP:** Extrae las propiedades necesarias (`Org`, `Project`, `RepoId`, `Branch`) desde el objeto JSON recibido.
2. **Autenticación:** Genera un token básico (`authToken`) para autenticar las solicitudes a la API de Azure DevOps.
3. **Listado de archivos en el repositorio:** Realiza una consulta en Azure DevOps para obtener una lista de archivos en el repositorio, excluyendo carpetas y ciertos directorios irrelevantes.
4. **Procesamiento de archivos:** Para cada archivo que cumpla las condiciones:
    - Obtiene el contenido del archivo mediante `GetFileContentAsync`.
    - Calcula el SHA del contenido del archivo utilizando la función `GetSha`.
    - Verifica si ya existe documentación para el archivo:
      - Si la documentación ya existe con el mismo SHA, se omite el procesamiento del archivo.
      - Si la documentación no existe o el SHA ha cambiado, se fragmenta el contenido del archivo en base al tamaño definido por `MaxFragmentLength`. Cada fragmento se utiliza dentro de un prompt para la API de OpenAI con el fin de generar su correspondiente documentación.
    - Comina los fragmentos y añade el SHA del archivo generado para finalizar la documentación.
    - Hace un commit en el repositorio de Azure DevOps con la nueva documentación.

**Valor de retorno:**  
Devuelve una respuesta HTTP con el estado (200 OK) y el mensaje `✅ Análisis completado y docs generados.`.

---

### **Método: `AnalyzeFileInFragments(string content, string filePath, string key, string model)`**

**Lenguaje:** C#

**Parámetros:**
- `string content`: Contenido completo del archivo a analizar.
- `string filePath`: La ruta del archivo.
- `string key`: Clave API de OpenAI para autenticación.
- `string model`: Modelo de lenguaje seleccionado en la API de OpenAI.

**Variables modificadas:**
- `sb` (StringBuilder): Contiene la documentación acumulada de los fragmentos generados.
- `start` (int): Índice actual del contenido que se está procesando.
- `fragmentIndex` (int): Número del fragmento actual en el texto dividido.
- `totalFragments` (int): Total de fragmentos obtenidos del archivo con base en su tamaño y el valor de `MaxFragmentLength`.

**Condiciones, validaciones o requisitos**:
- Descomposición del contenido del archivo en fragmentos de tamaño máximo definido por `MaxFragmentLength`.

**Descripción:**  
Este método divide el contenido del archivo en fragmentos según el tamaño máximo permitido y genera documentación utilizando el servicio de OpenAI para cada fragmento. Devuelve la documentación completa como un string.  

**Valor de retorno:**  
- Devuelve un string con toda la documentación generada para el archivo en formato Markdown.

---

### **Método: `BuildPrompt(string filePath, string codeFragment, int fragmentIndex, int totalFragments)`**

**Lenguaje:** C#

**Parámetros:**
- `string filePath`: Ruta del archivo para incluir en el prompt.
- `string codeFragment`: Fragmento del código que se debe analizar.
- `int fragmentIndex`: Índice del fragmento actual.
- `int totalFragments`: Número total de fragmentos generados y enumerados.

**Variables modificadas:** Ninguna.

**Descripción:**  
Este método construye un prompt basado en un fragmento de código y el archivo del que proviene. El prompt incluye instrucciones específicas para el análisis del código y generación de documentación técnica.

**Valor de retorno:**  
- Devuelve un string que contiene el prompt formateado.

---

### **Método: `AnalyzeWithOpenAI(string prompt, string key, string model)`**

**Lenguaje:** C#

**Parámetros:**
- `string prompt`: Texto que se le enviará a la API de OpenAI para el análisis.
- `string key`: Clave de API para autenticación con OpenAI.
- `string model`: Modelo de OpenAI que se utilizará para el análisis.

**Variables modificadas:** Ninguna.

**Condiciones, validaciones o requisitos**:
- La respuesta que regresa la API de OpenAI debe ser válida y contener la propiedad `choices->message->content`.

**Descripción:**  
Este método envía un prompt a la API de OpenAI para generar documentación técnica en formato Markdown basada en el contenido del código analizado. La configuración se establece para la comunicación segura con el endpoint de OpenAI utilizando el token de API proporcionado.

**Valor de retorno:**  
- El contenido del análisis generado por OpenAI en formato Markdown.

---

### **Método: `GetSha(string content)`**

**Lenguaje:** C#

**Parámetros:**
- `string content`: Contenido de un archivo para calcular su SHA.

**Variables modificadas:** Ninguna.

**Descripción:**  
Este método genera el hash SHA1 único de un contenido dado. Es útil para realizar verificaciones sobre la necesidad de procesar un archivo dependiendo de si su contenido ha cambiado.

**Valor de retorno:**  
- Devuelve un string que representa el hash SHA1 del contenido analizado.

---

### **Método: `TryGetFileContentAsync(string org, string project, string repoId, string branch, string path, string pat)`**

**Lenguaje:** C#

**Parámetros:**
- `string org`: Organización en Azure DevOps.
- `string project`: Proyecto en Azure DevOps.
- `string repoId`: Identificador del repositorio.
- `string branch`: Nombre de la rama.
- `string path`: Ruta del archivo en el repositorio.
- `string pat`: Token de acceso personal para Azure DevOps.

**Variables modificadas:** Ninguna.

**Descripción:**  
Este método intenta obtener el contenido de un archivo en el repositorio de Azure DevOps. En caso de errores en la solicitud, devuelve `null`.

**Valor de retorno:**
- Devuelve el contenido del archivo en formato string, o `null` si ocurre algún error.

---

### **Método: `GetFileContentAsync(string org, string project, string repoId, string branch, string path, string pat)`**

**Lenguaje:** C#

**Parámetros:**
- `string org`: Organización en Azure DevOps.
- `string project`: Proyecto en Azure DevOps.
- `string repoId`: Identificador del repositorio.
- `string branch`: Nombre de la rama.
- `string path`: Ruta del archivo.
- `string pat`: Token de acceso personal para Azure DevOps.

**Variables modificadas:** Ninguna.

**Condiciones, validaciones o requisitos**:
- Utiliza encabezados de autorización con un token generado mediante `pat`.
- Realiza una consulta HTTP GET al endpoint de Azure DevOps y asegura que la solicitud haya sido exitosa.

**Descripción:**  
Este método obtiene el contenido de un archivo específico en Azure DevOps. Se encarga de realizar la solicitud HTTP al servicio y devolver el contenido del archivo como un string.

**Valor de retorno:**  
- Devuelve un string que representa el contenido del archivo solicitado.

---

### **Método: `CommitFileAsync(string org, string project, string repoId, string branch, string filePath, string content, string pat)`**

**Lenguaje:** C#

**Parámetros:**
- `string org`: Organización en Azure DevOps.
- `string project`: Proyecto en Azure DevOps.
- `string repoId`: Identificador del repositorio.
- `string branch`: Nombre de la rama.
- `string filePath`: Ruta del archivo donde se realizará el commit.
- `string content`: Contenido nuevo que se agregará al archivo.
- `string pat`: Token de acceso personal para Azure DevOps.

**Variables modificadas:** Ninguna.

**Condiciones, validaciones o requisitos**:
- Requiere el último commit ID extraído con `GetLastCommitIdAsync`.
- Asegura un commit exitoso mediante la verificación del estado HTTP de la solicitud.

**Descripción:**  
Este método realiza un commit en un archivo específico del repositorio en Azure DevOps con el contenido generado. Construye el cuerpo de la solicitud con los detalles del commit y actualiza el archivo en la rama especificada.

**Valor de retorno:** No retorna ningún valor, pero asegura la ejecución del commit en Azure DevOps.

---

### **Método: `GetLastCommitIdAsync(string org, string project, string repoId, string branch, string pat)`**

**Lenguaje:** C#

**Parámetros:**
- `string org`: Organización en Azure DevOps.
- `string project`: Proyecto en Azure DevOps.
- `string repoId`: Identificador del repositorio.
- `string branch`: Nombre de la rama.
- `string pat`: Token de acceso personal para Azure DevOps.

**Variables modificadas:** Ninguna.

**Condiciones, validaciones o requisitos**:
- Requiere una solicitud exitosa para obtener información sobre el último commit desde el API de Azure DevOps.

**Descripción:**  
Este método obtiene el ID del último commit realizado en una rama específica dentro de Azure DevOps. Es utilizado para actualizar el repositorio correctamente durante el proceso de generación de la documentación.

**Valor de retorno:**  
- Devuelve el string con el último ID del commit de la rama especificada.

---

### **Estructura auxiliar: `RequestDto`**

**Lenguaje:** C#

**Propiedades:**  
- `string Org`: Organización en Azure DevOps.  
- `string Project`: Proyecto en Azure DevOps.  
- `string RepoId`: Identificador del repositorio.  
- `string Branch`: Nombre de la rama del repositorio.  

**Descripción:**  
Estructura de datos auxiliar utilizada para modelar el contenido de la solicitud HTTP entrante.

---

```


SHA:be6419d3d662d5eec031b24db512b0edb8fe04d0