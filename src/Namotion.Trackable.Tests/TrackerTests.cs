﻿using Namotion.Trackable.Model;
using System.Reactive.Subjects;

namespace Namotion.Trackable.Tests
{
    public class TrackerTests
    {
        [Fact]
        public void Test()
        {
            // Arrange
            var subject = new Subject<TrackedPropertyChange>();

            var tracker = new Tracker(null, null);
            var firstName = new TrackedProperty<string>("FirstName", "Rico", tracker, subject);
            var lastName = new TrackedProperty<string>("LastName", "Suter", tracker, subject);

            tracker.AddProperty(firstName);
            tracker.AddProperty(lastName);
            tracker.AddProperty(new DerivedTrackedProperty<string>("FullName", 
                () => $"{firstName.Value} {lastName.Value}", null, tracker, subject));

            var propertyChanges = new List<TrackedPropertyChange>();
            subject.Subscribe(propertyChanges.Add);

            // Act
            tracker.Properties["FirstName"].Value = "Foo";
            tracker.Properties["LastName"].Value = "Bar";

            var fullName = tracker.Properties["FullName"].Value;
            var fullName2 = tracker.Properties["FullName"].LastKnownValue;

            // Assert
            Assert.Equal(2, propertyChanges.Count);
            Assert.Equal("Foo", propertyChanges[0].Value);
            Assert.Equal(fullName, fullName2);
        }
    }
}
