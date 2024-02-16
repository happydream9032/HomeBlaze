﻿using HomeBlaze.Abstractions;
using HomeBlaze.Abstractions.Attributes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Threading;
using HomeBlaze.Abstractions.Sensors;
using HomeBlaze.Abstractions.Devices;
using HomeBlaze.Abstractions.Presentation;

namespace HomeBlaze.Shelly.Model
{
    public class ShellyCover : IThing, IIconProvider, IPowerConsumptionSensor, IRollerShutter
    {
        public string Id => Parent!.Id + "/cover";

        public string? Title => "Cover";

        public string IconName => "fas fa-bars";

        [ParentThing]
        public ShellyDevice? Parent { get; private set; }

        [State(Unit = StateUnit.Percent)]
        public decimal? Position => CurrentPosition / 100m;

        [State]
        public bool? IsMoving => PowerConsumption > 1;

        [State(Unit = StateUnit.Watt)]
        [JsonPropertyName("power")]
        public decimal? PowerConsumption { get; set; }

        [JsonPropertyName("state"), State]
        public string? State { get; set; }

        [JsonPropertyName("source"), State]
        public string? Source { get; set; }

        [JsonPropertyName("is_valid"), State]
        public bool? IsValid { get; set; }

        [JsonPropertyName("safety_switch"), State]
        public bool? HasSafetySwitch { get; set; }

        [JsonPropertyName("overtemperature"), State]
        public bool? HasOvertemperature { get; set; }

        [JsonPropertyName("stop_reason"), State]
        public string? StopReason { get; set; }

        [JsonPropertyName("last_direction"), State]
        public string? LastDirection { get; set; }

        [JsonPropertyName("current_pos")]
        public int? CurrentPosition { get; set; }

        [JsonPropertyName("calibrating"), State]
        public bool? IsCalibrating { get; set; }

        [JsonPropertyName("positioning"), State]
        public bool? Positioning { get; set; }

        [Operation]
        public async Task OpenAsync(CancellationToken cancellationToken)
        {
            await Parent!.CallHttpGetAsync("roller/0?go=open", cancellationToken);
        }

        [Operation]
        public async Task CloseAsync(CancellationToken cancellationToken)
        {
            // https://shelly-api-docs.shelly.cloud/gen2/ComponentsAndServices/Cover#http-endpoint-rollerid
            await Parent!.CallHttpGetAsync("roller/0?go=close", cancellationToken);
        }

        [Operation]
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await Parent!.CallHttpGetAsync("roller/0?go=stop", cancellationToken);
        }
    }
}