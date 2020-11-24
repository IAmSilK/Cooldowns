# Cooldowns
An OpenMod plugin which adds command cooldowns.

## Installation

To install, use the command `openmod install SilK.Cooldowns`.

To install RocketMod cooldowns too, use the command `openmod install SilK.Cooldowns.Rocket`.

## Configuration

The default config file contains one setting, `reloadPersistent:enabled` which is set to `true`.

This will enable cooldowns to persist throughout server restarts and OpenMod reloads by writing command execution times to a file for the user.

Command cooldowns are configured through the data config of OpenMod roles. The structure of the config is as follows:
```yaml
cooldowns:
- command: CommandId
  cooldown: cooldown time span
```

If you're using `SilK.Cooldowns.Rocket`, the command id will be the name found in `Commands.config.xml` prefixed by `Rocket.`.
For the `/heal` command, the id would be `Rocket.heal`.

The following example is a modified version of the default `openmod.roles.yaml` file to apply a cooldown of 30 seconds to the help command.
```yaml
roles:
- id: default
  priority: 0
  parents: []
  permissions:
  - OpenMod.Core:commands.help
  displayName: Default
  data:
    cooldowns:
    - command: OpenMod.Core.Commands.OpenModCommands.CommandHelp
      cooldown: 30 seconds
  isAutoAssigned: true
- id: vip
  priority: 1
  parents:
  - default
  permissions:
  - SomeKitsPlugin:kits.vip
  displayName: 
  data: {}
  isAutoAssigned: false
```

**If multiple cooldowns for the same command apply to a user, the cooldown originating from the role with the highest priority will be applied.**

The cooldown time span follows a format of multiple portions each defining a decimal number and its value. A list of these possible values are:
- Days - days, day, and d
- Hours - hours, hour, hrs, hr, h
- Minutes - minutes, minute, mins, min, m
- Seconds - seconds, second, secs, sec, s
- Milliseconds - milliseconds, millisecond, millis, milli, ms

All of the following are valid cooldown time spans:
- 10 days 20 hours 30 minutes
- 10 days, 20 hours, and 30 minutes
- 10d 20h 30m
- 10d20h30m
- 10.8 days
- 30 seconds 500 milliseconds

## Permissions
This plugin only contains one permission - `Cooldowns:immune`. This permission makes the inheritor immune to all cooldowns.
