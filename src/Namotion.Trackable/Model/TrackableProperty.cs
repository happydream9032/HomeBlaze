﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

using Namotion.Trackable.Attributes;
using Namotion.Trackable.Sourcing;

namespace Namotion.Trackable.Model;

public class TrackableProperty
{
    private readonly PropertyInfo _property;

    public TrackableProperty(
        PropertyInfo property,
        string path,
        Trackable parent,
        ITrackableContext context)
    {
        _property = property;

        Context = context;
        Path = path;
        Parent = parent;

        GetMethod = property.GetMethod;
        SetMethod = property.SetMethod;
    }

    // TODO: Make this thread-safe??
    [JsonIgnore]
    internal bool IsChangingFromSource { get; private set; }

    [JsonIgnore]
    public ITrackableContext Context { get; }

    public string Path { get; }

    [MemberNotNullWhen(true, nameof(AttributeMetadata))]
    public bool IsAttribute => AttributeMetadata != null;

    [JsonIgnore]
    public Trackable Parent { get; }

    [JsonIgnore]
    public MethodInfo? GetMethod { get; }

    [JsonIgnore]
    public MethodInfo? SetMethod { get; }

    public bool IsDerived => SetMethod == null;

    [JsonIgnore]
    public string PropertyName => _property.Name;

    [JsonIgnore]
    public Type PropertyType => _property.PropertyType;

    [JsonIgnore]
    public AttributeOfTrackableAttribute? AttributeMetadata => _property
        .GetCustomAttribute<AttributeOfTrackableAttribute>(true);

    [JsonIgnore]
    public TrackableProperty[] DependentProperties { get; internal set; } = Array.Empty<TrackableProperty>();

    public IEnumerable<string> DependentPropertyPaths => DependentProperties.Select(v => v.Path);

    public Dictionary<string, TrackableProperty> Attributes => Context
        .AllProperties
        .Where(v => v.AttributeMetadata?.PropertyName == _property.Name && v.Parent == Parent)
        .ToDictionary(v => v.AttributeMetadata!.AttributeName, v => v);

    [JsonExtensionData]
    public IDictionary<string, object?> ExtensionData { get; } = new Dictionary<string, object?>();

    public object? GetValue()
    {
        return _property.GetValue(Parent.Object);
    }

    public void SetValue(object? value)
    {
        _property.SetValue(Parent.Object, value);
    }

    public object? GetSourceValue()
    {
        var value = GetValue();
        return ConvertToSource(value);
    }

    public void SetValueFromSource(object? valueFromSource)
    {
        IsChangingFromSource = true;
        try
        {
            var currentValue = GetValue();
            var newValue = ConvertFromSource(valueFromSource);
            if (!Equals(currentValue, newValue))
            {
                SetValue(newValue);
            }
        }
        finally
        {
            IsChangingFromSource = false;
        }
    }

    private object? ConvertFromSource(object? value)
    {
        foreach (var attribute in _property
            .GetCustomAttributes(true)
            .OfType<IStateConverter>())
        {
            value = attribute.ConvertFromSource(value, _property.PropertyType, this);
        }

        return value;
    }

    private object? ConvertToSource(object? value)
    {
        foreach (var attribute in _property.GetCustomAttributes(true).OfType<IStateConverter>())
        {
            value = attribute.ConvertToSource(value, this);
        }

        return value;
    }
}