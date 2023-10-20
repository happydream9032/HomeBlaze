﻿using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace Namotion.Trackable.Sourcing;

public class CompositeTrackableContextSource : ITrackableContextSource
{
    private readonly IReadOnlyDictionary<string, ITrackableContextSource> _sources;
    private readonly string _separator;

    public CompositeTrackableContextSource(IReadOnlyDictionary<string, ITrackableContextSource> sources, string separator = ".")
    {
        _sources = sources
            .OrderByDescending(s => s.Key)
            .ToDictionary(s => s.Key, s => s.Value);

        _separator = separator;
    }

    public async Task<IReadOnlyDictionary<string, object?>> ReadAsync(IEnumerable<string> sourcePaths, CancellationToken cancellationToken)
    {
        var result = new List<KeyValuePair<string, object?>>();

        var groups = sourcePaths.GroupBy(p => _sources.First(s => p.StartsWith(s.Key + _separator)));
        foreach (var group in groups)
        {
            (var path, var source) = group.Key;
            if (path is not null && source is not null)
            {
                var innerSourcePaths = group.Select(p => p.Substring(path.Length + _separator.Length)); ;
                var sourceResult = await source.ReadAsync(innerSourcePaths, cancellationToken);
                result.AddRange(sourceResult.Select(p => new KeyValuePair<string, object?>(path + _separator + p.Key, p.Value)));
            }
        }

        return result.ToDictionary(p => p.Key, p => p.Value);
    }

    public async Task<IDisposable> SubscribeAsync(IEnumerable<string> sourcePaths, Action<string, object?> propertyUpdateAction, CancellationToken cancellationToken)
    {
        var disposables = new List<IDisposable>();

        var groups = sourcePaths.GroupBy(p => _sources.First(s => p.StartsWith(s.Key + _separator)));
        foreach (var group in groups)
        {
            (var path, var source) = group.Key;
            if (path is not null && source is not null)
            {
                var innerSourcePaths = group.Select(p => p.Substring(path.Length + _separator.Length)); ;
                disposables.Add(await source.SubscribeAsync(
                    innerSourcePaths,
                    (innerPath, value) => propertyUpdateAction(path + _separator + innerPath, value),
                    cancellationToken));
            }
        }

        return new CompositeDisposable(disposables);
    }

    public async Task WriteAsync(IReadOnlyDictionary<string, object?> propertyChanges, CancellationToken cancellationToken)
    {
        var result = new List<KeyValuePair<string, object?>>();

        var groups = propertyChanges.GroupBy(p => _sources.First(s => p.Key.StartsWith(s.Key + _separator)));
        foreach (var group in groups)
        {
            (var path, var source) = group.Key;
            if (path is not null && source is not null)
            {
                var innerSourcePaths = group.ToDictionary(p => p.Key.Substring(path.Length + _separator.Length), p => p.Value);
                await source.WriteAsync(innerSourcePaths, cancellationToken);
            }
        }
    }

    private class CompositeDisposable : IDisposable
    {
        private readonly IEnumerable<IDisposable> _disposables;

        public CompositeDisposable(IEnumerable<IDisposable> disposables)
        {
            _disposables = disposables;
        }

        public void Dispose()
        {
            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }
        }
    }
}
