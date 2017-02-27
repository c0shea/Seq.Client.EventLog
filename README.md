# Seq.Client.EventLog

[Seq](https://getseq.net/) is a fantastic tool for handling structured logs in .NET apps. There's a lot of value in having a centralized log repository that can ingest events from many sources.

The trouble, however, is that applications beyond your control write useful information to the Windows Event Logs. That's where the EventLog service comes in. Define the logs and filters you care about and the service takes care of ingesting them into Seq.

## Get Started

1. [Download the latest release](https://github.com/c0shea/Seq.Client.EventLog/releases) of Seq.Client.EventLog.
2. Extract it to your preferred install directory.
3. Edit the ```Seq.Client.EventLog.exe.config``` file, replacing the ```SeqUri``` with the URL of your Seq server. If you configured Seq to use API keys, also specify your key in the config file.
4. Edit the ```EventLogListeners.json``` file. There are sensible defaults in place, but you can change them you suit your needs.
5. From the command line, run ```Seq.Client.EventLog.exe /install```. This will install the Windows Service and set it to start automatically at boot.
6. From the command line, run ```net start Seq.Client.EventLog``` to start the service.
7. Click the refresh button in Seq as you wait anxiously for the events to start flooding in!

## Enriched Events

Events are ingested into Seq with a few useful properties that allow for easy searching.

![](https://raw.githubusercontent.com/c0shea/Seq.Client.EventLog/master/Screenshot.png)

## Event Log Listeners

The JSON config file allows for multiple listeners to be defined. Each one should be a new object in the array.

- **LogName**: The Windows Event Log name to listen to, e.g. Application, Security, etc.
- **MachineName**: If specified, the hostname of the machine to listen to events from for the log name. Omitting this value defaults to the machine the service is running on.
- **LogLevels**: A list of the integer severity levels of the entry. 1 = Error, 2 = Warning, 4 = Information, 8 = Success Audit, 16 = Failure Audit. If not specified, all events will be sent.
- **EventIds**: A list of the integer Event IDs of the entry. If not specified, all events will be sent.
- **Sources**: A list of source names to filter the events sent to Seq. If not specified, all events will be sent.
- **ProcessRetroactiveEntries**: If true, this will cause the service to send all matching event log entries that were written before the service started in addition to new entries. If false, only new entries written that meet the filter critera above since the service was started will be sent.

### Sample

```json
[
  {
    "LogName": "Application",
    "LogLevels": [ 1, 2 ],
    "ProcessRetroactiveEntries": true
  }
]
```