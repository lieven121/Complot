// Appended to the generated client (see nswag.json "extensionCode"). NSwag references this type for file uploads
// but does not emit it when ASP.NET Core describes the file as an IFormFile schema.
export interface FileParameter {
  data: Blob
  fileName: string
}
