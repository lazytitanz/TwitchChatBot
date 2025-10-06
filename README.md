# TwitchChatBot

A Visual Basic .NET Twitch chat bot built on .NET 8.0 with a secure plugin architecture. Uses TwitchLib for Twitch integration and Spectre.Console for terminal UI.

## Features

- **Plugin Architecture**: Extend functionality without modifying core code
- **Security Scanner**: Analyzes plugins for risky operations before loading
- **Permission System**: Role-based hierarchy (Everyone < Subscriber < VIP < Moderator < Broadcaster)
- **Cooldown Management**: Per-user and global command cooldowns
- **Job Scheduling**: Timer-based scheduled tasks
- **Isolated Plugin Loading**: Each plugin loads in its own AssemblyLoadContext

## Quick Start

### Prerequisites

- .NET 8.0 SDK
- Twitch account for the bot
- OAuth token from https://twitchapps.com/tmi/

### Configuration

1. Update `appsettings.json` with your bot credentials:

```json
{
  "Bot": {
    "Username": "YourBotUsername",
    "OAuthToken": "oauth:your_token_here",
    "Channel": "your_channel_name"
  }
}
```

### Build and Run

```bash
# Build the project
dotnet build

# Run the bot
dotnet run --project TwitchChatBot\TwitchChatBot.vbproj

# Build in Release mode
dotnet build -c Release

# Test the security scanner
dotnet run --project TwitchChatBot\TwitchChatBot.vbproj -- --test-scanner
```

## Plugin Development

Plugins are external .NET assemblies that extend bot functionality. See the full documentation in `CLAUDE.md` for details.

### Plugin Requirements

1. Reference TwitchChatBot.exe
2. Implement `IBotPlugin` interface
3. Include `plugin.json` manifest declaring capabilities
4. Place in `bin/{Configuration}/net8.0/Plugins/{PluginName}/`

### Plugin Manifest Example

```json
{
  "name": "MyPlugin",
  "version": "1.0.0",
  "sdk": "1.0.0",
  "author": "Your Name",
  "publisher": "YourPublisher",
  "capabilities": ["network"],
  "allowedApis": ["Newtonsoft.Json"]
}
```

### Available Capabilities

- `network`: Network operations (HTTP, sockets)
- `disk`: File I/O and registry access
- `process`: Process manipulation and P/Invoke
- `apiHosting`: Hosting HTTP/TCP servers (trusted publishers only)
- `reflectionEmit`: Dynamic code generation (trusted publishers only)
- `dynamicLoading`: Assembly.Load operations (trusted publishers only)

## Architecture

### Core Components

- **Bot.vb**: Main bot class managing TwitchLib client and lifecycle
- **CommandRouter.vb**: Routes messages to commands, enforces permissions and cooldowns
- **PluginManager.vb**: Discovers and loads plugins with security scanning
- **BotSDK.vb**: Public API for plugins
- **Scheduler.vb**: Timer-based job scheduler

### Services

- **PermissionService.vb**: Role-based permission checks
- **CooldownService.vb**: Command cooldown tracking
- **PluginSecurityScanner.vb**: IL code analysis for security risks

## Dependencies

- TwitchLib 3.5.3
- Spectre.Console 0.49.1
- Newtonsoft.Json 13.0.3
- Mono.Cecil 0.11.6
- Microsoft.Extensions.Configuration 8.0.0

## License

[Add your license here]