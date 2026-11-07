*Author: Byron*

*Subject: outline I/O facilities for the application*

`namespace trifecta.io`

## ComponentLog

### Data Structure

- class ComponentLog
  - <singleton>
  - types
    - using ThreadId = int
  - contains
    - class LifespanToken : IDisposable
    - iTextLogSink Sink
    - Dictionary<ThreadId, string> ComponentNames
    - Monitor
  - performs
    - void Configure(ITextLogSink)
    - LifespanToken Register(string ComponentName)

## FileLogSink

### Data Structure

- class FileLog : ITextLogSink
  - contains
    - System.IO.StreamWriter Sink

## Prompt

A console interface for the user to query state and issue commands.

## CommandParser

Loads a sequence of commands from a structured file (e.g., JSON). Each command indicates a delay since the last
command, an action to take, and an optional tag string. Commands simulate manual oversight and administration of
the factory and are routed to static singletons (e.g., actor.Directory.instance()._____()).

## Interfaces

- interface ITextLog
  - void Log(string)

- interface ITextLogSink
  - void Log(string)

