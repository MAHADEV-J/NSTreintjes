using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisplayAMap
{
    internal class ClassAbstractions
    {
        public class TrainInfo
        {
            public int? TreinNummer { get; set; }
            public string? RitId { get; set; }
            public double? Lat { get; set; }
            public double? Lng { get; set; }
            public double? Snelheid { get; set; }
            public double? Richting { get; set; }
            public double? HorizontaleNauwkeurigheid { get; set; }
            public string? Type { get; set; }
            public string? Bron { get; set; }
            public bool? AllowCrowdReporting { get; set; }
            public string? Source { get; set; }
        }

        public class Payload
        {
            public List<TrainInfo>? Treinen { get; set; }
            public List<TrainStop>? Stops { get; set; }
        }
        public class RootObject
        {
            public Payload? Payload { get; set; }
        }

        public class TrainStop
        {
            public string? Id { get; set; }
            public Stop? Stop { get; set; }
            public List<string>? PreviousStopId { get; set; }
            public List<string>? NextStopId { get; set; }
            public string? Status { get; set; }
            public List<ArrivalDeparture>? Arrivals { get; set; }
            public List<ArrivalDeparture>? Departures { get; set; }
            public ActualStock? ActualStock { get; set; }
            public PlannedStock? PlannedStock { get; set; }
            public List<object>? PlatformFeatures { get; set; }
            public List<object>? CoachCrowdForecast { get; set; }
        }

        public class Stop
        {
            public string? Name { get; set; }
            public double Lng { get; set; }
            public double Lat { get; set; }
            public string? CountryCode { get; set; }
            public string? UicCode { get; set; }
        }

        public class ArrivalDeparture
        {
            public Product Product { get; set; }
            public Stop Origin { get; set; }
            public Stop Destination { get; set; }
            public string? PlannedTime { get; set; }
            public string? ActualTime { get; set; }
            public int DelayInSeconds { get; set; }
            public string? PlannedTrack { get; set; }
            public string? ActualTrack { get; set; }
            public bool Cancelled { get; set; }
            public string? CrowdForecast { get; set; }
            public List<string> StockIdentifiers { get; set; }
        }

        public class Product
        {
            public string? Number { get; set; }
            public string? CategoryCode { get; set; }
            public string? ShortCategoryName { get; set; }
            public string? LongCategoryName { get; set; }
            public string? OperatorCode { get; set; }
            public string OperatorName { get; set; }
            public string Type { get; set; }
        }

        public class ActualStock
        {
            public string TrainType { get; set; }
            public int NumberOfSeats { get; set; }
            public int NumberOfParts { get; set; }
            public List<TrainPart> TrainParts { get; set; }
            public bool HasSignificantChange { get; set; }
        }

        public class PlannedStock
        {
            public string TrainType { get; set; }
            public int NumberOfSeats { get; set; }
            public int NumberOfParts { get; set; }
            public List<TrainPart> TrainParts { get; set; }
            public bool HasSignificantChange { get; set; }
        }

        public class TrainPart
        {
            public string StockIdentifier { get; set; }
            public List<string> Facilities { get; set; }
            public Image Image { get; set; }
        }

        public class Image
        {
            public string Uri { get; set; }
        }
    }
}
