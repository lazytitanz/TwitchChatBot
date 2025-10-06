Imports System.IO
Imports System.Reflection
Imports System.Runtime.Loader
Imports Spectre.Console
Imports Newtonsoft.Json

''' <summary>
''' Manages loading, initialization, and lifecycle of bot plugins with isolated dependency resolution.
''' Each plugin is loaded from its own subfolder with its dependencies isolated via AssemblyLoadContext.
''' </summary>
Public Class PluginManager
    Private ReadOnly loadedPlugins As New List(Of LoadedPluginInfo)
    Private ReadOnly pluginsFolder As String
    Private sdk As BotSDK

    Public Sub New(pluginsFolder As String)
        Me.pluginsFolder = pluginsFolder
    End Sub

    ''' <summary>
    ''' Initialize the plugin manager with bot SDK
    ''' </summary>
    Public Sub Initialize(botSDK As BotSDK)
        Me.sdk = botSDK
        AnsiConsole.MarkupLine("  [dim]└─[/] [blue]Plugin Manager initialized[/]")
    End Sub

    ''' <summary>
    ''' Load and initialize all plugins from subfolder structure: Plugins/{PluginName}/*Plugin.dll
    ''' </summary>
    Public Sub LoadPlugins()
        If Not Directory.Exists(pluginsFolder) Then
            AnsiConsole.MarkupLine($"[yellow]⚠ Plugins folder not found: {pluginsFolder.EscapeMarkup()}[/]")
            Return
        End If

        Dim pluginFolders = Directory.GetDirectories(pluginsFolder)
        If pluginFolders.Length = 0 Then
            AnsiConsole.MarkupLine("[dim]No plugin folders found in Plugins directory[/]")
            Return
        End If

        AnsiConsole.WriteLine()
        Dim loadPanel As New Panel("[aqua]Scanning plugin folders...[/]")
        loadPanel.Header = New PanelHeader("[aqua]🔌 Plugin Loader[/]", Justify.Left)
        loadPanel.Border = BoxBorder.Rounded
        loadPanel.BorderColor(Color.Aqua)
        AnsiConsole.Write(loadPanel)

        For Each pluginFolder In pluginFolders
            Try
                LoadPluginFromFolder(pluginFolder)
            Catch ex As Exception
                AnsiConsole.MarkupLine($"  [red]✗ Failed to load from {Path.GetFileName(pluginFolder).EscapeMarkup()}: {ex.Message.EscapeMarkup()}[/]")
            End Try
        Next

        If loadedPlugins.Count > 0 Then
            DisplayLoadedPluginsTable()
        Else
            AnsiConsole.MarkupLine("[yellow]No plugins loaded[/]")
        End If
    End Sub

    Private Sub DisplayLoadedPluginsTable()
        AnsiConsole.WriteLine()
        Dim table As New Table()
        table.Border = TableBorder.Rounded
        table.BorderColor(Color.Green)
        table.AddColumn(New TableColumn("[bold]Plugin[/]").Centered())
        table.AddColumn(New TableColumn("[bold]Version[/]").Centered())
        table.AddColumn(New TableColumn("[bold]Author[/]").Centered())
        table.AddColumn(New TableColumn("[bold]Status[/]").Centered())

        For Each pluginInfo In loadedPlugins
            table.AddRow(
                $"[cyan]{pluginInfo.Plugin.Name.EscapeMarkup()}[/]",
                $"[dim]{pluginInfo.Plugin.Version.EscapeMarkup()}[/]",
                $"[dim]{pluginInfo.Plugin.Author.EscapeMarkup()}[/]",
                "[green]✓ Loaded[/]"
            )
        Next

        Dim summaryPanel As New Panel(table)
        summaryPanel.Header = New PanelHeader($"[green]✓ {loadedPlugins.Count} Plugin(s) Loaded[/]", Justify.Center)
        summaryPanel.Border = BoxBorder.Rounded
        summaryPanel.BorderColor(Color.Green)
        AnsiConsole.Write(summaryPanel)
    End Sub

    ''' <summary>
    ''' Load a plugin from a specific folder, searching for *Plugin.dll files
    ''' </summary>
    Private Sub LoadPluginFromFolder(pluginFolder As String)
        ' Find all DLLs matching *Plugin.dll pattern
        Dim pluginDlls = Directory.GetFiles(pluginFolder, "*Plugin.dll", SearchOption.TopDirectoryOnly)

        If pluginDlls.Length = 0 Then
            AnsiConsole.MarkupLine($"[dim]No *Plugin.dll found in {Path.GetFileName(pluginFolder)}[/]")
            Return
        End If

        For Each pluginDll In pluginDlls
            Try
                LoadPlugin(pluginDll, pluginFolder)
            Catch ex As BadImageFormatException
                ' Skip native DLLs silently
            Catch ex As Exception
                AnsiConsole.MarkupLine($"[red]Failed to load {Path.GetFileName(pluginDll)}: {ex.Message}[/]")
            End Try
        Next
    End Sub

    ''' <summary>
    ''' Load a single plugin DLL with isolated AssemblyLoadContext for dependency resolution
    ''' </summary>
    Private Sub LoadPlugin(pluginPath As String, pluginFolder As String)
        ' === SECURITY SCANNING ===
        Dim pluginFileName = Path.GetFileName(pluginPath)

        ' 1. Check for plugin.json manifest
        Dim manifestPath = Path.Combine(pluginFolder, "plugin.json")
        If Not File.Exists(manifestPath) Then
            AnsiConsole.MarkupLine($"  [red]✗ {pluginFileName.EscapeMarkup()}[/] - Missing manifest")
            Return
        End If

        ' 2. Load and parse manifest
        Dim manifest As PluginManifest
        Try
            Dim manifestJson = File.ReadAllText(manifestPath)
            manifest = JsonConvert.DeserializeObject(Of PluginManifest)(manifestJson)
        Catch ex As Exception
            AnsiConsole.MarkupLine($"  [red]✗ {pluginFileName.EscapeMarkup()}[/] - Invalid manifest")
            Return
        End Try

        ' 3. Scan the plugin assembly for security issues
        AnsiConsole.MarkupLine($"  [dim]→[/] [cyan]Scanning {pluginFileName.EscapeMarkup()}...[/]")
        Dim scanResult = PluginSecurityScanner.ScanPlugin(pluginPath, manifest)

        ' 4. Display scan results
        Dim riskColor = If(scanResult.RiskLevel = RiskLevel.High, "red",
                          If(scanResult.RiskLevel = RiskLevel.Medium, "yellow", "green"))
        AnsiConsole.MarkupLine($"    [dim]Risk:[/] [{riskColor}]{scanResult.RiskLevel}[/] [dim](Score: {scanResult.RiskScore})[/]")

        ' 5. Reject if not approved
        If Not scanResult.IsApproved Then
            AnsiConsole.MarkupLine($"  [red]✗ REJECTED:[/] {scanResult.DenialReason.EscapeMarkup()}")
            Return
        End If

        ' 6. Warn on high risk (but allow if capabilities are declared)
        If scanResult.RiskLevel = RiskLevel.High Then
            AnsiConsole.MarkupLine($"    [yellow]⚠ High risk - capabilities declared[/]")
        End If

        ' Create isolated load context for this plugin
        Dim loadContext As New PluginLoadContext(pluginFolder)

        Try
            ' Load the plugin assembly in the isolated context
            Dim assembly As Assembly = loadContext.LoadFromAssemblyPath(pluginPath)

            ' Safely get types (handle ReflectionTypeLoadException)
            Dim types = SafeGetTypes(assembly)

            For Each type In types
                ' Only instantiate concrete types implementing IBotPlugin
                If type.GetInterface("IBotPlugin") IsNot Nothing AndAlso
                   Not type.IsInterface AndAlso
                   Not type.IsAbstract Then

                    Dim plugin As IBotPlugin = CType(Activator.CreateInstance(type), IBotPlugin)

                    ' Set plugin folder path in SDK before initialization
                    sdk.SetPluginFolder(pluginFolder)

                    ' Initialize plugin with SDK
                    plugin.Initialize(sdk)

                    ' Track the loaded plugin with its context
                    loadedPlugins.Add(New LoadedPluginInfo(plugin, loadContext, pluginPath))

                    AnsiConsole.MarkupLine($"  [green]✓ {plugin.Name.EscapeMarkup()}[/] [dim]v{plugin.Version.EscapeMarkup()} by {plugin.Author.EscapeMarkup()}[/]")
                    Return ' Only load one plugin per DLL
                End If
            Next

            ' No plugin found - unload context
            loadContext.Unload()

        Catch ex As Exception
            ' If loading fails, unload the context
            loadContext.Unload()
            Throw
        End Try
    End Sub

    ''' <summary>
    ''' Safely get types from assembly, handling ReflectionTypeLoadException
    ''' </summary>
    Private Function SafeGetTypes(assembly As Assembly) As Type()
        Try
            Return assembly.GetTypes()
        Catch ex As ReflectionTypeLoadException
            ' Return only the types that loaded successfully
            Return ex.Types.Where(Function(t) t IsNot Nothing).ToArray()
        End Try
    End Function

    ''' <summary>
    ''' Shutdown all loaded plugins and unload their AssemblyLoadContexts
    ''' </summary>
    Public Sub ShutdownPlugins()
        If loadedPlugins.Count = 0 Then Return

        For Each pluginInfo In loadedPlugins
            Try
                pluginInfo.Plugin.Shutdown()
                AnsiConsole.MarkupLine($"  [dim]└─[/] [yellow]Shutdown: {pluginInfo.Plugin.Name.EscapeMarkup()}[/]")
            Catch ex As Exception
                AnsiConsole.MarkupLine($"  [red]✗ Error shutting down {pluginInfo.Plugin.Name.EscapeMarkup()}: {ex.Message.EscapeMarkup()}[/]")
            End Try

            ' Unload the plugin's AssemblyLoadContext
            Try
                pluginInfo.LoadContext.Unload()
            Catch ex As Exception
                AnsiConsole.MarkupLine($"  [red]✗ Error unloading context for {pluginInfo.Plugin.Name.EscapeMarkup()}[/]")
            End Try
        Next

        loadedPlugins.Clear()
        AnsiConsole.MarkupLine("  [dim]└─[/] [yellow]All plugins unloaded[/]")
    End Sub

    ''' <summary>
    ''' Reload all plugins (shutdown, unload, and reload from disk)
    ''' </summary>
    Public Sub Reload()
        AnsiConsole.MarkupLine("[yellow]Reloading plugins...[/]")
        ShutdownPlugins()

        ' Force garbage collection to ensure contexts are cleaned up
        GC.Collect()
        GC.WaitForPendingFinalizers()

        LoadPlugins()
    End Sub

    ''' <summary>
    ''' Get list of loaded plugins
    ''' </summary>
    Public Function GetLoadedPlugins() As IBotPlugin()
        Return loadedPlugins.Select(Function(p) p.Plugin).ToArray()
    End Function
End Class

''' <summary>
''' Tracks a loaded plugin instance along with its AssemblyLoadContext and file path
''' </summary>
Friend Class LoadedPluginInfo
    Public ReadOnly Property Plugin As IBotPlugin
    Public ReadOnly Property LoadContext As PluginLoadContext
    Public ReadOnly Property PluginPath As String

    Public Sub New(plugin As IBotPlugin, loadContext As PluginLoadContext, pluginPath As String)
        Me.Plugin = plugin
        Me.LoadContext = loadContext
        Me.PluginPath = pluginPath
    End Sub
End Class

''' <summary>
''' Custom AssemblyLoadContext for isolated plugin loading with dependency resolution from plugin folder.
''' Allows plugins to ship their own versions of dependencies without conflicts.
''' </summary>
Friend Class PluginLoadContext
    Inherits AssemblyLoadContext

    Private ReadOnly pluginFolder As String
    Private ReadOnly resolver As AssemblyDependencyResolver

    Public Sub New(pluginFolder As String)
        MyBase.New(isCollectible:=True) ' Collectible allows unloading
        Me.pluginFolder = pluginFolder

        ' Find any DLL in the folder to use as resolver base
        Dim dllFiles = Directory.GetFiles(pluginFolder, "*.dll", SearchOption.TopDirectoryOnly)
        If dllFiles.Length > 0 Then
            resolver = New AssemblyDependencyResolver(dllFiles(0))
        End If
    End Sub

    Protected Overrides Function Load(assemblyName As AssemblyName) As Assembly
        ' First, try to resolve from plugin folder using resolver
        If resolver IsNot Nothing Then
            Dim assemblyPath = resolver.ResolveAssemblyToPath(assemblyName)
            If assemblyPath IsNot Nothing Then
                Return LoadFromAssemblyPath(assemblyPath)
            End If
        End If

        ' Try to find the assembly directly in the plugin folder
        Dim localPath = Path.Combine(pluginFolder, assemblyName.Name & ".dll")
        If File.Exists(localPath) Then
            Return LoadFromAssemblyPath(localPath)
        End If

        ' Fall back to default context (for shared assemblies like BotSDK, TwitchLib, etc.)
        Return Nothing
    End Function

    Protected Overrides Function LoadUnmanagedDll(unmanagedDllName As String) As IntPtr
        ' Try to resolve unmanaged DLL from plugin folder
        If resolver IsNot Nothing Then
            Dim libraryPath = resolver.ResolveUnmanagedDllToPath(unmanagedDllName)
            If libraryPath IsNot Nothing Then
                Return LoadUnmanagedDllFromPath(libraryPath)
            End If
        End If

        ' Try plugin folder directly
        Dim localPath = Path.Combine(pluginFolder, unmanagedDllName)
        If File.Exists(localPath) Then
            Return LoadUnmanagedDllFromPath(localPath)
        End If

        ' Fall back to default resolution
        Return IntPtr.Zero
    End Function
End Class
