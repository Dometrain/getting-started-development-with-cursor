# Pub/sub

Live snapshots between a publisher and every API replica. These commands are not a stored key.

- `PUBLISH` delivers to exact `SUBSCRIBE` clients and matching `PSUBSCRIBE` patterns. It is not stored and not written to the WAL. https://ruviodb.com/llms/commands.txt
- This project runs one shard. `shards` defaults to 1. Classic `PUBLISH` reaches subscribers on every shard in both port modes. https://ruviodb.com/llms/configuration.txt
- The C# client sends `CLUSTER SLOTS` and routes each command to the shard that owns the key. `GetHashSlot(key)` is that 0–16383 slot, including `{hash tags}`. https://ruviodb.com/llms/clients/csharp.txt
- `SUBSCRIBE` takes exact channels on this shard. After a subscription the connection accepts `SUBSCRIBE`, `UNSUBSCRIBE`, `PSUBSCRIBE`, `PUNSUBSCRIBE`, `SSUBSCRIBE`, `SUNSUBSCRIBE`, and `PING`. https://ruviodb.com/llms/commands.txt
- `UNSUBSCRIBE` with no arguments drops every exact classic channel on this connection and leaves patterns in place. https://ruviodb.com/llms/commands.txt
- `SPUBLISH` is delivered only to subscribers of that channel on this slot. It is not stored and not durable. https://ruviodb.com/llms/commands.txt
- `SSUBSCRIBE` puts the connection in Pub/Sub mode. There is no pattern subscribe on that command. https://ruviodb.com/llms/commands.txt
- A full subscriber queue disconnects that subscriber. https://ruviodb.com/llms/commands.txt
- A queued `PUBLISH` inside `MULTI` is sent after `EXEC` is recorded. `DISCARD` sends nothing. `SUBSCRIBE` is not queueable inside `MULTI`. https://ruviodb.com/llms/commands.txt
- Because `PUBLISH` is not written to the WAL, a durability profile does not store live messages. https://ruviodb.com/llms/commands.txt
- The C# client opens a second TCP socket for subscribe. `GET`, `SET`, and `PUBLISH` stay on the command socket. https://ruviodb.com/llms/clients/csharp.txt
- A dropped socket is opened again before the next command. Pub/Sub channels are subscribed again on the new socket. The command that discovered the drop is not sent again. https://ruviodb.com/llms/clients/csharp.txt
- The ASP.NET Core registration does the same resubscribe when `Reconnect` is true (the default). https://ruviodb.com/llms/clients/csharp-aspnetcore.txt
- `RuvioConnectionNotice` includes whether the socket that dropped is the Pub/Sub connection. https://ruviodb.com/llms/clients/csharp-aspnetcore.txt

```csharp
await db.SubscribeAsync("news", msg => Console.WriteLine(msg.Payload));
await db.PublishAsync("news", "hello");
```

https://ruviodb.com/llms/clients/csharp.txt

- `SubscribeExpiredAsync` and `SubscribeEvictedAsync` are keyspace notices, not the snapshot channel. They need server `notify_keyspace_events`. https://ruviodb.com/llms/clients/csharp.txt
- Keyspace notices are not written to the WAL and are not replayed to later subscribers. `PSUBSCRIBE` patterns do not receive them. https://ruviodb.com/llms/configuration.txt
