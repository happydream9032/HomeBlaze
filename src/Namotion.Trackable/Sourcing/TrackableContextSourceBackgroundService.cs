﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;

namespace Namotion.Trackable.Sourcing;

public class TrackableContextSourceBackgroundService<TTrackable> : BackgroundService
    where TTrackable : class
{
    private readonly TrackableContext<TTrackable> _trackableContext;
    private readonly ITrackableContextSource _source;
    private readonly ILogger _logger;

    private readonly TimeSpan _bufferTime;
    private readonly TimeSpan _retryTime;

    private HashSet<string>? _initializedProperties;

    public TrackableContextSourceBackgroundService(
        TrackableContext<TTrackable> trackableContext,
        ITrackableContextSource source,
        ILogger logger,
        TimeSpan? bufferTime = null,
        TimeSpan? retryTime = null)
    {
        _trackableContext = trackableContext;
        _source = source;
        _logger = logger;

        _bufferTime = bufferTime ?? TimeSpan.FromMilliseconds(8);
        _retryTime = retryTime ?? TimeSpan.FromSeconds(10);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // TODO: Currently newly added properties/trackable are not automatically tracked/subscribed to

                var sourcePaths = _trackableContext
                    .AllProperties // TODO: find better way below
                    .Where(p => _trackableContext.Trackables.Any(t => t.ParentProperty == p) == false) // only properties with objects which are not trackable/trackables (value objects)
                    .Select(p => p.TryGetSourcePath())
                    .Where(p => p is not null)
                    .ToList();

                lock (this)
                {
                    _initializedProperties = new HashSet<string>();
                }

                // subscribe first and mark all properties as initialized which are updated before the read has completed 
                using var disposable = await _source.SubscribeAsync(sourcePaths!, UpdatePropertyValueFromSource, stoppingToken);

                // read all properties (subscription during read will later be ignored)
                var initialValues = await _source.ReadAsync(sourcePaths!, stoppingToken);
                lock (this)
                {
                    // ignore properties which have been updated via subscription
                    foreach (var value in initialValues
                        .Where(v => !_initializedProperties.Contains(v.Key)))
                    {
                        UpdatePropertyValueFromSource(value.Key, value.Value);
                    }

                    _initializedProperties = null;
                }

                await _trackableContext
                    .Where(change => !change.IsChangingFromSource() && change.Property.TryGetSourcePath() != null)
                    .BufferChanges(_bufferTime)
                    .Where(changes => changes.Any())
                    .ForEachAsync(async changes =>
                    {
                        var values = changes
                           .ToDictionary(
                               c => c.Property.TryGetSourcePath()!,
                               c => c.Property.ConvertToSource(c.Value));

                        await _source.WriteAsync(values, stoppingToken);
                    }, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to listen for changes.");
                await Task.Delay(_retryTime);
            }
        }
    }

    protected void UpdatePropertyValueFromSource(string sourcePath, object? value)
    {
        MarkPropertyAsInitialized(sourcePath);

        var property = _trackableContext
            .AllProperties
            .FirstOrDefault(v => v.TryGetSourcePath() == sourcePath);

        if (property != null)
        {
            property.SetValueFromSource(value);
        }
    }

    private void MarkPropertyAsInitialized(string sourcePath)
    {
        if (_initializedProperties != null)
        {
            lock (this)
            {
                if (_initializedProperties != null)
                {
                    _initializedProperties.Add(sourcePath);
                }
            }
        }
    }
}