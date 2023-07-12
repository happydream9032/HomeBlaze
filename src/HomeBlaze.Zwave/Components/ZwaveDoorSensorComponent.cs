﻿using HomeBlaze.Abstractions.Sensors;
using ZWave.CommandClasses;

namespace HomeBlaze.Zwave.Components
{
    public class ZwaveDoorSensorComponent : ZwaveClassComponent, IDoorSensor
    {
        protected override string Class => "DoorSensor";

        // see https://github.com/zwave-js/node-zwave-js/blob/master/packages/config/config/notifications.json#L406

        public DoorState? DoorState =>
            ParentNotification.Event == NotificationState.WindowDoorOpen ? Abstractions.Sensors.DoorState.Open :
            ParentNotification.Event == NotificationState.WindowDoorClosed ? Abstractions.Sensors.DoorState.Close :
            null;

        public ZwaveNotificationComponent ParentNotification { get; }

        public ZwaveDoorSensorComponent(ZwaveNotificationComponent parent)
            : base(parent.ParentDevice)
        {
            ParentNotification = parent;
        }
    }
}
