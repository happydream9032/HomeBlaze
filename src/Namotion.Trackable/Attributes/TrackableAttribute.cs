﻿using Namotion.Trackable.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace Namotion.Trackable.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class TrackableAttribute : Attribute
{
    public IEnumerable<Model.Trackable> CreateTrackablesForProperty(PropertyInfo property, ITrackableContext context, Model.Trackable parent, object parentProxy)
    {
        if (property.GetCustomAttribute<TrackableAttribute>(true) != null)
        {
            var propertyPath = GetPath(parent.Path, property);

            var trackableProperty = CreateTrackableProperty(property, propertyPath, parent, context);
            parent.Properties.Add(trackableProperty);

            if (property.GetCustomAttributes(true).Any(a => a is RequiredAttribute ||
                                                            a.GetType().FullName == "System.Runtime.CompilerServices.RequiredMemberAttribute") &&
                property.PropertyType.IsClass &&
                property.PropertyType.FullName?.StartsWith("System.") == false)
            {
                var child = context.Create(property.PropertyType);

                foreach (var childThing in context.CreateThings(child, propertyPath, trackableProperty))
                    yield return childThing;

                property.SetValue(parentProxy, child);
            }
        }

    }

    protected virtual TrackableProperty CreateTrackableProperty(PropertyInfo property, string path, Model.Trackable parent, ITrackableContext context)
    {
        return new TrackableProperty(property, path, parent, context);
    }

    private string GetPath(string basePath, PropertyInfo propertyInfo)
    {
        return (!string.IsNullOrEmpty(basePath) ? basePath + "." : "") + propertyInfo.Name;
    }
}