﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reactive.Linq;

using Castle.DynamicProxy;
using Namotion.Trackable.Model;
using Namotion.Trackable.Validation;

namespace Namotion.Trackable;

public class TrackableInterceptor : ITrackableInterceptor
{
    private readonly object _lock = new();
    private readonly ICollection<ITrackableContext> _trackableContexts = new HashSet<ITrackableContext>();
   
    private readonly IEnumerable<ITrackablePropertyValidator> _propertyValidators;

    [ThreadStatic]
    private static Stack<Tuple<TrackedProperty, List<TrackedProperty>>>? _touchedProperties;

    public TrackableInterceptor(IEnumerable<ITrackablePropertyValidator> propertyValidators, ITrackableContext trackableContext)
    {
        _propertyValidators = propertyValidators;
        _trackableContexts.Add(trackableContext);
    }

    public void Intercept(IInvocation invocation)
    {
        ITrackableContext[] trackableContexts;
        lock (_lock)
        {
            if (invocation.Method?.Name == nameof(ITrackable.AddTrackableContext) &&
                invocation.Method.DeclaringType?.IsAssignableTo(typeof(ITrackable)) == true)
            {
                _trackableContexts.Add((ITrackableContext)invocation.Arguments[0]);
                return;
            }
            else if (invocation.Method?.Name == nameof(ITrackable.RemoveTrackableContext) &&
                     invocation.Method.DeclaringType?.IsAssignableTo(typeof(ITrackable)) == true)
            {
                _trackableContexts.Remove((ITrackableContext)invocation.Arguments[0]);
                return;
            }

            trackableContexts = _trackableContexts.ToArray();
        }

        foreach (var trackableContext in _trackableContexts)
        {
            if (invocation.InvocationTarget is ITrackable trackable)
            {
                if (trackableContext.Object == null)
                {
                    trackableContext.InitializeProxy(trackable);
                }
            }

            var getProperty = trackableContext
                .AllProperties
                .FirstOrDefault(v => v.Parent.Object == invocation.InvocationTarget &&
                                     v.GetMethod?.Name == invocation.Method?.Name);
           
            var setProperty = trackableContext
                .AllProperties
                .FirstOrDefault(v => v.Parent.Object == invocation.InvocationTarget &&
                                     v.SetMethod?.Name == invocation.Method?.Name);

            if (setProperty != null)
            {
                var errors = _propertyValidators
                    .SelectMany(v => v.Validate(setProperty, invocation.Arguments[0], trackableContext))
                    .ToArray();

                if (errors.Any())
                {
                    throw new ValidationException(string.Join("\n", errors.Select(e => e.ErrorMessage)));
                }

                var previousValue = setProperty.GetValue();
                var newValue = invocation.Arguments[0];
                if (!Equals(previousValue, newValue))
                {
                    invocation.Proceed();

                    if (previousValue != null && (previousValue is ITrackable || previousValue is ICollection))
                    {
                        trackableContext.Detach(previousValue);
                    }

                    if (newValue != null && (newValue is ITrackable || newValue is ICollection))
                    {
                        trackableContext.Attach(setProperty, newValue);
                    }
                }
            }
            else if (getProperty != null)
            {
                if (_touchedProperties == null)
                {
                    _touchedProperties = new Stack<Tuple<TrackedProperty, List<TrackedProperty>>>();
                }

                if (getProperty.IsDerived)
                {
                    var dependencies = new List<TrackedProperty>();

                    _touchedProperties!.Push(new Tuple<TrackedProperty, List<TrackedProperty>>(getProperty, dependencies));

                    invocation.Proceed();
                    getProperty.DependentProperties = dependencies.ToArray();

                    _touchedProperties.Pop();
                }
                else
                {
                    invocation.Proceed();
                }

                if (_touchedProperties.Any())
                {
                    _touchedProperties.Peek().Item2.Add(getProperty);
                }
            }
            else
            {
                invocation.Proceed();
            }

            if (setProperty != null)
            {
                trackableContext.MarkVariableAsChanged(setProperty);
            }
        }
    }
}
