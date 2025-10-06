''' <summary>
''' Interface that all bot plugins must implement to integrate with the bot SDK.
''' Provides lifecycle methods for plugin initialization and cleanup.
''' </summary>
Public Interface IBotPlugin
    ''' <summary>Plugin name for identification</summary>
    ReadOnly Property Name As String

    ''' <summary>Plugin version for compatibility checking</summary>
    ReadOnly Property Version As String

    ''' <summary>Plugin author information</summary>
    ReadOnly Property Author As String

    ''' <summary>
    ''' Initialize the plugin with access to bot services
    ''' </summary>
    Sub Initialize(sdk As BotSDK)

    ''' <summary>
    ''' Clean up plugin resources when bot shuts down
    ''' </summary>
    Sub Shutdown()
End Interface