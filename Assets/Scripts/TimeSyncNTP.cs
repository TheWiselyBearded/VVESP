using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text;

public class TimeSyncNTP {

    public static long startTime, endTime;
    public static long timeBetweenExecution;
    public static DateTime dt;
    public static DateTimeOffset datetimeoffset;

    #region NTP_VARS
    private static DateTime ntpTime;
    private static readonly object ntpTimeLock = new object();
    protected static string ntpServer = "time.windows.com"; // "pool.ntp.org"; // NTP server address        
    public List<double> latencyMeasurements = new List<double>();   // List to store the latency measurements
    #endregion

    public static long GetTimeSyncNTP() {
        MeasureLatency();
        return datetimeoffset.ToUnixTimeMilliseconds();
    }

    public static void GetNtpTimeCheck() {
        // Send NTP request to NTP server
        var ntpData = new byte[48];
        ntpData[0] = 0x1B;
        var addresses = Dns.GetHostEntry(ntpServer).AddressList;
        var ipEndPoint = new IPEndPoint(addresses[0], 123);
        using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)) {
            socket.Connect(ipEndPoint);
            socket.Send(ntpData);
            socket.Receive(ntpData);
            socket.Close();
        }

        // Calculate NTP time
        ulong intPart = (ulong)ntpData[40] << 24 | (ulong)ntpData[41] << 16 | (ulong)ntpData[42] << 8 | (ulong)ntpData[43];
        ulong fractPart = (ulong)ntpData[44] << 24 | (ulong)ntpData[45] << 16 | (ulong)ntpData[46] << 8 | (ulong)ntpData[47];
        var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);
        var networkDateTime = (new DateTime(1900, 1, 1)).AddMilliseconds((long)milliseconds);

        lock (ntpTimeLock) {
            ntpTime = networkDateTime.ToLocalTime();
        }
        Console.WriteLine("NTP time: " + ntpTime.ToLocalTime());
    }

    public static void MeasureLatency() {
        startTime = SystemDataFlowMeasurements.GetUnixTS();
        // Get NTP time
        DateTime localNtpTime;
        if (endTime != 0) {
            timeBetweenExecution = startTime - endTime;
            //TimeSpan offset = TimeZoneInfo.Local.GetUtcOffset(localNtpTime);
            datetimeoffset = datetimeoffset.AddMilliseconds(timeBetweenExecution);
            //dt = DateTimeOffset.FromUnixTimeMilliseconds(timeBetweenExecution + localNtpTime.Millisecond).LocalDateTime;
            //dt = new DateTime((timeBetweenExecution + localNtpTime.Millisecond) * 10000L + DateTime.MinValue.Ticks);
            //localNtpTime = dt; // TODO: Find a way to add datetimes
        } else {
            lock (ntpTimeLock) {
                localNtpTime = ntpTime;
                datetimeoffset = new DateTimeOffset(localNtpTime);
            }
        }
        DateTimeOffset curr = new DateTimeOffset(DateTime.Now);
        // Measure latency
        var latency = (curr - datetimeoffset).TotalMilliseconds;
            
        //Console.WriteLine($"Latency {latency} ms, time between {timeBetweenExecution}, Current {curr} NTP {datetimeoffset}");
        endTime = SystemDataFlowMeasurements.GetUnixTS();
    }

    public static DateTime GetNtpTime(string ntpServer) {
        startTime = SystemDataFlowMeasurements.GetUnixTS();
        // Request the current NTP time from the server
        var ntpData = new byte[48];
        ntpData[0] = 0x1B;
        var address = Dns.GetHostEntry(ntpServer).AddressList[0];
        var endpoint = new IPEndPoint(address, 123);
        using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)) {
            socket.Connect(endpoint);
            socket.Send(ntpData);
            socket.Receive(ntpData);
        }

        // Calculate the number of seconds since 1900
        ulong intPart = (ulong)ntpData[40] << 24 | (ulong)ntpData[41] << 16 | (ulong)ntpData[42] << 8 | (ulong)ntpData[43];
        ulong fractPart = (ulong)ntpData[44] << 24 | (ulong)ntpData[45] << 16 | (ulong)ntpData[46] << 8 | (ulong)ntpData[47];
        var milliseconds = (intPart * 1000 + fractPart * 1000 / 0x100000000L);
        var ntpTime = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds((long)milliseconds);

        // Display the NTP time
        endTime = SystemDataFlowMeasurements.GetUnixTS();
        Console.WriteLine($"NTP time: {ntpTime.ToLocalTime()}, Latency {endTime - startTime}");
        return ntpTime;
    }

}
