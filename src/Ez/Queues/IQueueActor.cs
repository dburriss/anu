using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

using Orleans;

namespace Ez.Queues;

public interface IQueueActor : IGrainWithStringKey
{
    Task Enqueue(QueueMessage message);
    Task<QueueMessage?> Dequeue();
    Task RegisterSubscriber(string subscriberId);
    Task RemoveSubscriber(string subscriberId);
}

[GenerateSerializer]
public class QueueMessage
{
    [Id(0)]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Id(1)]
    public required string Content { get; set; }
    [Id(2)]
    public DateTimeOffset CreatedTimestamp { get; set; } = DateTimeOffset.UtcNow;
}

public class QueueActor : Grain, IQueueActor
{
    private readonly ConcurrentQueue<QueueMessage> _messages = new();
    private readonly HashSet<string> _subscribers = new();

    public async Task Enqueue(QueueMessage message)
    {
        _messages.Enqueue(message);
        await NotifySubscribers(message);
    }

    public Task<QueueMessage?> Dequeue()
    {
        if (_messages.TryDequeue(out var message))
        {
            return Task.FromResult<QueueMessage?>(message);
        }

        return Task.FromResult<QueueMessage?>(null);
    }

    public Task RegisterSubscriber(string subscriberId)
    {
        _subscribers.Add(subscriberId);
        return Task.CompletedTask;
    }

    public Task RemoveSubscriber(string subscriberId)
    {
        _subscribers.Remove(subscriberId);
        return Task.CompletedTask;
    }

    private async Task NotifySubscribers(QueueMessage message)
    {
        if (_subscribers.Count == 0) return;

        foreach (var subscriberId in _subscribers)
        {
            var subscriber = GrainFactory.GetGrain<IQueueSubscriber>(subscriberId);
            await subscriber.OnMessageReceived(message);
        }
    }
}

public interface IQueueSubscriber : IGrainWithStringKey
{
    Task OnMessageReceived(QueueMessage message);
}

public interface IQueueClient
{
    Task SendMessage(string queueName, QueueMessage message);
    Task RegisterHandler(string queueName, Func<QueueMessage, Task> handler);
    Task<QueueMessage?> Dequeue(string queueName);
    IAsyncEnumerable<QueueMessage> DequeueBatch(string queueName, ushort batchSize = 100);
}

public class QueueClient : IQueueClient
{
    private readonly IClusterClient _orleansClient;

    // Stores active handlers for each queue
    private readonly ConcurrentDictionary<string, Func<QueueMessage, Task>> _handlers = new();

    public QueueClient(IClusterClient orleansClient)
    {
        _orleansClient = orleansClient;
    }

    public async Task SendMessage(string queueName, QueueMessage message)
    {
        var queueActor = _orleansClient.GetGrain<IQueueActor>(queueName);
        await queueActor.Enqueue(message);
    }

    public async Task<QueueMessage?> Dequeue(string queueName)
    {
        var queueActor = _orleansClient.GetGrain<IQueueActor>(queueName);
        return await queueActor.Dequeue();
    }

    public async IAsyncEnumerable<QueueMessage> DequeueBatch(string queueName, ushort batchSize = 100)
    {
        var count = 0;
        var queueActor = _orleansClient.GetGrain<IQueueActor>(queueName);
        while (count < batchSize)
        {
            var msg = await queueActor.Dequeue();
            if(msg == null)
                break;
            count = count + 1;
            yield return msg;
        }
    }

    public async Task RegisterHandler(string queueName, Func<QueueMessage, Task> handler)
    {
        // Register the handler
        if (!_handlers.TryAdd(queueName, handler))
        {
            throw new InvalidOperationException($"Handler already registered for queue '{queueName}'.");
        }

        // Register as a subscriber with the queue actor
        var queueActor = _orleansClient.GetGrain<IQueueActor>(queueName);
        // var subscriberId = Guid.NewGuid().ToString();
        // await queueActor.RegisterSubscriber(subscriberId);

        // Poll for messages in a background task
        _ = Task.Run(async () =>
        {
            while (_handlers.ContainsKey(queueName))
            {
                var message = await queueActor.Dequeue();
                if (message != null)
                {
                    try
                    {
                        await handler(message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing message: {ex}");
                    }
                }
                else
                {
                    // Wait briefly before polling again to reduce unnecessary calls
                    await Task.Delay(100);
                }
            }
        });
    }
}
