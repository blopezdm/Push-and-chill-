# CreateDocumentation.cs

## Descripción Funcional
Este archivo define una función de Azure que analiza los repositorios de GitHub y genera documentación técnica para sus archivos, transformando su contenido en un conjunto de archivos en formato Markdown. La documentación generada se almacena directamente en el repositorio de GitHub. Este proceso también incluye análisis global del proyecto y puede dividir archivos en fragmentos si son demasiado grandes. 

La principal funcionalidad se implementa en la clase `CreateDocumentation`, utilizando el cliente HTTP para interactuar con las API de GitHub y OpenAI GPT-4.

---

## Descripción Técnica

### Clase: `CreateDocumentation`
Esta clase implementa la lógica para la generación de documentación técnica desde repositorios GitHub. Utiliza las siguientes dependencias:
- **HttpClient**: Es invocado para realizar solicitudes a GitHub.
- **ILogger**: Se utiliza para registrar información sobre el proceso.
- **Microsoft Azure Functions Attributes**: Define la entrada para la función.

---

#### Método: `CreateDocumentation` (Constructor)
- **Lenguaje:** C#
- **Parámetros:**
  - `loggerFactory`: Objeto para la creación de un logger.
- **Variables Modificadas:**
  - `_httpClient`: Inicializado con una nueva instancia de `HttpClient`.
  - `_logger`: Logger inicializado mediante `loggerFactory`.
- **Descripción:** 
  Este constructor inicializa las instancias de `HttpClient` y `ILogger` necesarias para ejecutar las operaciones del procesamiento y generación de documentación, asegurando que los componentes estén listos para una interacción eficiente con las APIs.
- **Valor de Retorno:** No aplica (constructor).

---

#### Método: `Run`
- **Lenguaje:** C#
- **Parámetros:**
  - `req`: Objeto de tipo `HttpRequestData` que contiene los datos de la solicitud HTTP.
- **Variables Modificadas:**
  - `_httpClient.DefaultRequestHeaders.Authorization`: Añade un token de autenticación para las peticiones a GitHub.
  - `_httpClient.DefaultRequestHeaders.UserAgent`: Especifica el agente de usuario.
  - `files`: Lista de archivos obtenidos del repositorio GitHub.
- **Condiciones, Validaciones o Requisitos:**
  - `AuthorizationLevel.Function` define el nivel de autorización para ejecutar esta función.
  - Extrae el JSON del cuerpo de la solicitud para determinar las propiedades `repo`, `branch` y `complete`. 
  - Valida el acceso a las APIs de GitHub mediante las claves: `GITHUB_TOKEN` y `OPENAI_API_KEY`.
  - Filtra únicamente archivos con extensiones `.cs`, `.js`, `.html` ignorando ciertas rutas o clases específicas.
- **Descripción:**
  1. Obtiene datos JSON del cuerpo de la solicitud (propiedades como el nombre de repositorio `repo`, rama `branch` y si el análisis es completo `complete`).
  2. Recupera una lista de archivos del repositorio utilizando la API de GitHub, basado en el nombre del repositorio y la rama proporcionados.
  3. Filtra los archivos relevantes y genera la documentación técnica de cada archivo utilizando el método `GenerateDocumentationWithFragments` si son muy grandes.
  4. Actualiza o sube los archivos en formato Markdown generados a la misma rama del repositorio correspondiente.
  5. Si el análisis es completo, genera un archivo global README.md del repositorio y lo sube a la ruta raíz del mismo.
- **Valor de Retorno:** 
  Una respuesta HTTP de tipo `HttpResponseData` que marca si el análisis fue completado con éxito.

---

#### Método: `GenerateDocumentationWithFragments`
- **Lenguaje:** C#
- **Parámetros:**
  - `filePath`: Ruta del archivo dentro del repositorio.
  - `content`: Contenido completo del archivo en formato texto.
  - `apiKey`: Llave de API para acceder a OpenAI.
- **Variables Modificadas:** Ninguna.
- **Condiciones, Validaciones o Requisitos:**
  - Calcula el tamaño para dividir el contenido en fragmentos si excede el límite de caracteres definido por `MaxFragmentLength` (15,000 caracteres).
  - Los fragmentos son procesados individualmente.
- **Descripción:** 
  1. Divide el contenido del archivo si excede el límite de caracteres.
  2. Por cada fragmento, construye un prompt que contiene instrucciones específicas para generar documentación técnica.
  3. Para cada fragmento, genera un archivo Markdown solicitando información al método `GenerateDocumentationAsync`.
  4. Combina la documentación técnica de los fragmentos para retornar un único archivo en formato Markdown asociado al contenido completo del archivo original.
- **Valor de Retorno:** Un string que representa la documentación en formato Markdown del archivo completo.

---

#### Método: `BuildPrompt`
- **Lenguaje:** C#
- **Parámetros:**
  - `filePath`: Ruta del archivo dentro del repositorio.
  - `codeFragment`: Fragmento de código.
  - `fragmentIndex`: Índice actual de fragmento.
  - `totalFragments`: Número total de fragmentos.
- **Variables Modificadas:** Ninguna.
- **Condiciones, Validaciones o Requisitos:**
  - Crea un prompt con instrucciones específicas para generar documentación técnica de un fragmento específico.
- **Descripción:** 
  1. Define normas claras sobre cómo debe generarse la documentación técnica.
  2. Incluye detalles específicos como el índice y cantidad total de fragmentos procesados.
  3. Combina el contenido del código con las instrucciones para optimizar la generación de la documentación de OpenAI.
- **Valor de Retorno:** Un string con el prompt enriquecido para procesar documentación técnica.

---

#### Método: `GenerateDocumentationAsync`
- **Lenguaje:** C#
- **Parámetros:**
  - `prompt`: Contiene las instrucciones y fragmentos de código para procesar con OpenAI.
  - `apiKey`: Llave de acceso para invocar la API OpenAI GPT-4.
- **Variables Modificadas:** Ninguna.
- **Condiciones, Validaciones o Requisitos:**
  - Realiza una solicitud HTTP POST a la API de OpenAI con el prompt preformado.
  - La respuesta debe tener un formato válido (200 OK) para ser procesada y retornar la documentación.
- **Descripción:** 
  1. Configura el cliente HTTP para conectarse al endpoint de OpenAI.
  2. Formatea el payload asociado al *prompt* y envía la solicitud HTTP.
  3. Recupera la respuesta con la documentación en formato texto.
- **Valor de Retorno:** Un string con la documentación técnica generada por OpenAI para el código de entrada.

---

## Dependencias Internas y Externas
### Internas
- Métodos auxiliares:
  - `GenerateDocumentationWithFragments`
  - `BuildPrompt`
  - `GenerateDocumentationAsync`
- Constantes: 
  - `MaxFragmentLength` (15,000 caracteres).

### Externas
- **System.Net.Http.Headers**: Para configurar las cabeceras HTTP.
- **System.Text.Json**: Para el manejo de datos JSON (serialización/deserialización).
- **Azure Functions SDK**: Permite definir la función `CreateDocumentation` como una función de Azure compatible con `HttpTrigger`.
- **Microsoft Extensions**:
  - `Logging`: Para registrar mensajes e información del proceso.
  - `HttpClient`: Cliente para ejecutar solicitudes HTTP.
- APIs externas:
  - GitHub: Para interactuar con repositorios, descargar archivos y actualizarlos.
  - OpenAI GPT-4: Para generar documentación técnica basada en contexto y fragmentos de código.

--- 

## Patrones de Arquitectura y Diseño
- **Microservicios (Azure Functions)**: La función `CreateDocumentation` implementa el paradigma de funciones autónomas en el contexto de servicios en la nube, siguiendo el modelo de microservicios.
- **Cliente-Servidor RESTful**: El archivo utiliza un enfoque de cliente-servidor para comunicarse con la API de GitHub y OpenAI a través de HTTP.
- **Separación de responsabilidades**: Cada método está diseñado con una responsabilidad específica para garantizar modularidad, legibilidad y mantenimiento.

--- 
### Fragmento 2 de 2: Documentación técnica de las funciones/métodos

#### Métodos definidos:

---

### 1. Método: **GenerateDocumentationGeneralAsync**

- **Lenguaje:** C#  
- **Parámetros:**
  - `repoSummary`  
    - Texto que contiene la descripción del repositorio GitHub que se analizará.  
  - `apiKey`  
    - Clave API utilizada para enviar solicitudes al endpoint de OpenAI.  

- **Variables modificadas:**
  - Ninguna variable global es modificada.  
  - Se crean varias variables locales para procesar y enviar datos al endpoint, como `endpoint`, `deployment`, `apiVersion`, `payload`, entre otras.  

- **Condiciones/Validaciones:**  
  - El método utiliza la función `response.EnsureSuccessStatusCode()` para garantizar que la llamada HTTP al endpoint se complete correctamente. En caso de fallo, se lanzará una excepción.  

- **Función anidada:** Ninguna anidación visible.  

- **Descripción:**  
  Este método asíncrono construye un prompt para solicitar documentación técnica y un diagrama **Mermaid** mediante una llamada al servicio OpenAI. El proceso consiste en:  
  1. Construir un prompt detallado que solicita el análisis técnico y arquitectónico del repositorio proporcionado. Este incluye reglas específicas para la generación del diagrama **Mermaid** compatible con Markdown.  
  2. Definir parámetros como el payload, que incluye mensajes dirigidos al modelo de inteligencia artificial y su configuración (como temperatura y tokens máximos).  
  3. Enviar la solicitud HTTP POST al endpoint Azure OpenAI, definiendo el destino exacto en base a un `deployment` y `apiVersion`.  
  4. Procesar la respuesta JSON proveniente del servicio, extrayendo la propiedad `choices[0].message.content`.  
  5. Retornar como resultado un texto generado por el modelo, que incluye tanto la documentación técnica como el diagrama **Mermaid**.  

- **Valor de retorno:**  
  - Retorna un string (`string`) que contiene la respuesta generada por el modelo OpenAI. Este texto incluye:  
    - Resumen técnico.  
    - Descripción de arquitectura.  
    - Detalle de las tecnologías usadas.  
    - Diagrama **Mermaid** compatible con GitHub.  
    - Conclusión técnica.  

---

### Flujo General del Código:
1. **Declaración del prompt:** El prompt textual generado incluye preguntas relacionadas con la arquitectura, tecnologías, dependencias y diagrama propuesto.  
2. **Definición de parámetros básicos:** Se configuran las variables clave necesarias para la comunicación con el servicio OpenAI:
   - `endpoint`: URL del servicio.  
   - `deployment`: Identificador del modelo desplegado.  
   - `apiVersion`: Versión API específica.  
3. **Construcción del payload:** Se crea un objeto serializable JSON que incluye mensajes y parámetros de configuración de modelo IA (como `max_tokens`, `temperature` y otros).  
4. **HTTP Request:** Se utiliza una instancia de `HttpClient` para enviar la información a través de un POST al endpoint especificado.  
5. **Validación de respuesta:** La función garantiza que el resultado sea exitoso (`EnsureSuccessStatusCode`).  
6. **Procesamiento y retorno:** Analiza el contenido JSON resultante y retorna su contenido textual principal, que contiene la documentación solicitada.

---

### Notas Técnicas Adicionales:
- **Librerías usadas:**  
  - `System.Net.Http` para manejo y envío de solicitudes HTTP.  
  - `System.Text.Json` para manipulación y parseo de contenido JSON.  
  - `System` para el manejo de objetos estándar como cadenas e iteradores.  

- **Requisito clave:**  
  El usuario debe proporcionar un valor válido para `apiKey`. Sin esta clave, el método no podrá realizar la solicitud al servicio OpenAI Azure.  

- **Ejecución asíncrona:**  
  Este método depende de `HttpClient` y operaciones asincrónicas (`await`), lo que permite implementarlo en entornos con ejecución no bloqueante (ideal para aplicaciones web o servicios backend).


SHA:7f17bb4bd3ff568a414d7359e9ab9a43949990a3