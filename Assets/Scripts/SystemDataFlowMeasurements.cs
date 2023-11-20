using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Newtonsoft;
using Newtonsoft.Json;

public static class SystemDataFlowMeasurements {
    public static string deviceType;
    public static Stopwatch stopwatch;
    public static long StartTime, EndTime;
    public static MeasurementCollector measurements;
    public static bool initialized;

    public static void Init() {
        measurements = new MeasurementCollector();
        initialized = true;
        TimeSyncNTP.GetNtpTimeCheck();
        TimeSyncNTP.MeasureLatency();
    }

    public static void WriteToDisk(string filename="data_") {
        lock (measurements) {
            if (measurements.measurements.Count == 0) {
                Console.WriteLine("No measurements to write.");
                return;
            }
            if (filename == "data_" && (deviceType != "" || deviceType != null)) filename = deviceType + "_" + filename;
            // Create a copy of the measurements collection before serializing it
            var measurementsCopy = new MeasurementCollector();
            measurementsCopy.measurements.AddRange(measurements.measurements);
            string json_measurements = Newtonsoft.Json.JsonConvert.SerializeObject(measurementsCopy);
            string documents_path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\";
            // check if folder for date exists
            string directoryPath = documents_path + @"VolumetricStreamingDataAnalysis\" + DateTime.Now.ToString("MM_dd") + @"\" + deviceType + @"\";
            if (!Directory.Exists(directoryPath)) {
                Directory.CreateDirectory(directoryPath);
            }
            // create if not
            // write into folder
            string full_fn =  directoryPath + filename + DateTime.Now.ToString("MM_dd_mm_ss_fff");
            string file_path = full_fn+ ".json";
            Console.WriteLine($"Size of {measurements.measurements.Count} and last element values {measurements.measurements[measurements.measurements.Count-1]}");
            Console.WriteLine($"writing file to path {file_path}");
            System.IO.File.WriteAllText(file_path, json_measurements);
        } 
    }

    /// <summary>
    /// Get unix timestamp in milliseconds.
    /// </summary>
    /// <returns></returns>
    public static long GetUnixTS() {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return now.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// For stopwatch, we use Ticks because it provides finer precision for 
    /// measurements resulting in the nanosecond range.
    /// </summary>
    /// <param name="elapsedTicks"></param>
    /// <returns></returns>
    public static long TickToNS(long elapsedTicks) {
        long nanosecPerTick = (1000L * 1000L * 1000L) / Stopwatch.Frequency;
        return elapsedTicks * nanosecPerTick;
    }
    public static double ConvertNStoMS(long t) {
        float converter = 0.000001f;
        return Convert.ToDouble(t) * Convert.ToDouble(converter);
    }

    public static void StartTimer() {
        stopwatch = Stopwatch.StartNew();
        StartTime = TickToNS(stopwatch.ElapsedTicks);
    }


    public static void EndTimer() {
        stopwatch.Stop();
        EndTime = TickToNS(stopwatch.ElapsedTicks) - StartTime;
        //Debug.Log($"Time diff between start {StartTime} is {EndTime} and converted is {ConvertNStoMS(EndTime)}");
    }

    public static void AddMeasurement(Measurement _m) {
        //Console.WriteLine("Added measurement");
        measurements.AddMeasurement(_m);
    }

}

[Serializable]
public class MeasurementCollector {
    public List<Measurement> measurements;

    public MeasurementCollector() {
        measurements = new List<Measurement>();
    }

    public void AddMeasurement(Measurement measurement) {
        measurements.Add(measurement);
    }

}

//[Serializable]
//public class ConsumerNetworkBuffer : SystemMeasurement {
//    public ConsumerNetworkBuffer() { base.name = "ConsumerNetworkBuffer"; }
//}



[Serializable]
public class Measurement {
    public long StartTime { get; set; }
    public long EndTime { get; set; }
    public long InputBytes { get; set; }
    public long OutputBytes { get; set; }
    public string Name { get; set; }

    public long ntpTimeSync { get; set; }

    public int FrameNumber { get; set; }

    public Measurement(string name) { this.Name = name; }
    public Measurement(string name, int frameNumber) {
        this.Name = name;
        this.FrameNumber = frameNumber;
    }

    public void SetProcessStart() { StartTime = SystemDataFlowMeasurements.GetUnixTS(); }
    public void SetProcessEnd() { EndTime = SystemDataFlowMeasurements.GetUnixTS(); }
    public void SetBytesConsumed(long _bs) { 
        InputBytes = _bs;
        OutputBytes = _bs;
    }
    /// <summary>
    /// in circumstances where output size is different than input size
    /// </summary>
    public void SetOutputBytes(long _bs) { OutputBytes = _bs; }
}