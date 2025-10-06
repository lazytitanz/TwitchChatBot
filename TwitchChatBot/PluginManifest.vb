Imports Newtonsoft.Json

Public Class PluginManifest
    <JsonProperty("name")>
    Public Property Name As String

    <JsonProperty("version")>
    Public Property Version As String

    <JsonProperty("sdk")>
    Public Property SdkVersion As String

    <JsonProperty("author")>
    Public Property Author As String

    <JsonProperty("publisher")>
    Public Property Publisher As String

    <JsonProperty("capabilities")>
    Public Property Capabilities As PluginCapabilities

    <JsonProperty("commands")>
    Public Property Commands As List(Of String)

    <JsonProperty("allowedApis")>
    Public Property AllowedApis As List(Of String)

    Public Sub New()
        Commands = New List(Of String)()
        AllowedApis = New List(Of String)()
    End Sub
End Class

Public Class PluginCapabilities
    <JsonProperty("network")>
    Public Property Network As Boolean

    <JsonProperty("disk")>
    Public Property Disk As Boolean

    <JsonProperty("process")>
    Public Property Process As Boolean

    <JsonProperty("apiHosting")>
    Public Property ApiHosting As Boolean

    <JsonProperty("reflectionEmit")>
    Public Property ReflectionEmit As Boolean

    <JsonProperty("dynamicLoading")>
    Public Property DynamicLoading As Boolean

    Public Sub New()
        Network = False
        Disk = False
        Process = False
        ApiHosting = False
        ReflectionEmit = False
        DynamicLoading = False
    End Sub
End Class
