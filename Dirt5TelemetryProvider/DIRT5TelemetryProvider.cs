//MIT License
//
//Copyright(c) 2019 PHARTGAMES
//
//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:
//
//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.
//
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.
//
using SimFeedback.log;
using SimFeedback.telemetry;
using System;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Numerics;
using NoiseFilters;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace DIRT5Telemetry
{
    /// <summary>
    /// Wreckfest Telemetry Provider
    /// </summary>
    public sealed class DIRT5TelemetryProvider : AbstractTelemetryProvider
    {
        private bool isStopped = true;                                  // flag to control the polling thread
        private Thread t;

        /// <summary>
        /// Default constructor.
        /// Every TelemetryProvider needs a default constructor for dynamic loading.
        /// Make sure to call the underlying abstract class in the constructor.
        /// </summary>
        public DIRT5TelemetryProvider() : base()
        {
            Author = "PEZZALUCIFER";
            Version = "v1.0";
            BannerImage = @"img\banner_DIRT5.png"; // Image shown on top of the profiles tab
            IconImage = @"img\DIRT5.jpg";  // Icon used in the tree view for the profile
            TelemetryUpdateFrequency = 100;     // the update frequency in samples per second
        }

        /// <summary>
        /// Name of this TelemetryProvider.
        /// Used for dynamic loading and linking to the profile configuration.
        /// </summary>
        public override string Name { get { return "dirt5"; } }

        public override void Init(ILogger logger)
        {
            base.Init(logger);
            Log("Initializing DIRT5TelemetryProvider");
        }

        /// <summary>
        /// A list of all telemetry names of this provider.
        /// </summary>
        /// <returns>List of all telemetry names</returns>
        public override string[] GetValueList()
        {
            return GetValueListByReflection(typeof(DIRT5API));
        }

        /// <summary>
        /// Start the polling thread
        /// </summary>
        public override void Start()
        {
            if (isStopped)
            {
                LogDebug("Starting DIRT5TelemetryProvider");

                t = new Thread(Run);
                t.Start();
            }
        }


        /// <summary>
        /// Stop the polling thread
        /// </summary>
        public override void Stop()
        {
            LogDebug("Stopping DIRT5TelemetryProvider");
            isStopped = true;


            if (t != null) t.Join();
        }

        /// <summary>
        /// The thread funktion to poll the telemetry data and send TelemetryUpdated events.
        /// </summary>
        private void Run()
        {
            
            isStopped = false;
            
            DIRT5API lastTelemetryData = new DIRT5API();
            lastTelemetryData.Reset();
            Matrix4x4 lastTransform = Matrix4x4.Identity;
            bool lastFrameValid = false;
            Vector3 lastVelocity = Vector3.Zero;
            float lastYaw = 0.0f;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            
            NestedSmooth accXSmooth = new NestedSmooth( 3, 6, 0.5f );
            NestedSmooth accYSmooth = new NestedSmooth( 3, 6, 0.5f );
            NestedSmooth accZSmooth = new NestedSmooth( 3, 6, 0.5f );
            
            KalmanFilter velXFilter = new KalmanFilter(1, 1, 0.02f, 1, 0.02f, 0.0f);
            KalmanFilter velZFilter = new KalmanFilter(1, 1, 0.02f, 1, 0.02f, 0.0f);

            NoiseFilter velXSmooth = new NoiseFilter(6, 0.5f);
            NoiseFilter velZSmooth = new NoiseFilter(6, 0.5f);

            KalmanFilter yawRateFilter = new KalmanFilter(1, 1, 0.02f, 1, 0.02f, 0.0f);
            NoiseFilter yawRateSmooth = new NoiseFilter(6, 0.5f);

            NoiseFilter pitchFilter = new NoiseFilter(3);
            NoiseFilter rollFilter = new NoiseFilter(3);
            NoiseFilter yawFilter = new NoiseFilter(3);

            KalmanFilter posXFilter = new KalmanFilter( 1, 1, 0.02f, 1, 0.1f, 0.0f );
            KalmanFilter posYFilter = new KalmanFilter( 1, 1, 0.02f, 1, 0.1f, 0.0f );
            KalmanFilter posZFilter = new KalmanFilter( 1, 1, 0.02f, 1, 0.1f, 0.0f );

            NestedSmooth posXSmooth = new NestedSmooth( 12, 6, 0.5f );
            NestedSmooth posYSmooth = new NestedSmooth( 12, 6, 0.5f );
            NestedSmooth posZSmooth = new NestedSmooth( 12, 6, 0.5f );

            NoiseFilter slipAngleSmooth = new NoiseFilter(6, 0.25f);

            int readSize = 4 * 4 * 4;
            byte[] readBuffer;

            MemoryMappedFile mmf = null;

            while (!isStopped)
            {
                try
                {
                    float dt = (float)sw.ElapsedMilliseconds / 1000.0f;

                    while (true)
                    {
                        try
                        {
                            mmf = MemoryMappedFile.OpenExisting("Dirt5MatrixProvider");

                            if (mmf != null)
                                break;
                            else
                                Thread.Sleep(1000);
                        }
                        catch (FileNotFoundException)
                        {
                            Thread.Sleep(1000);
                        }
                    }

                    Mutex mutex = Mutex.OpenExisting("Dirt5MatrixProviderMutex");
                    mutex.WaitOne();
                    using (MemoryMappedViewStream stream = mmf.CreateViewStream())
                    {
                        BinaryReader reader = new BinaryReader(stream);

                        readBuffer = reader.ReadBytes(readSize);
                    }
                    mutex.ReleaseMutex();

                    float[] floats = new float[4 * 4];

                    Buffer.BlockCopy(readBuffer, 0, floats, 0, readBuffer.Length);

                    Matrix4x4 transform = new Matrix4x4(floats[0], floats[1], floats[2], floats[3]
                                                        , floats[4], floats[5], floats[6], floats[7]
                                                        , floats[8], floats[9], floats[10], floats[11]
                                                        , floats[12], floats[13], floats[14], floats[15]);



                    Vector3 rht = new Vector3(transform.M11, transform.M12, transform.M13);
                    Vector3 up = new Vector3(transform.M21, transform.M22, transform.M23);
                    Vector3 fwd = new Vector3(transform.M31, transform.M32, transform.M33);

                    float rhtMag = rht.Length();
                    float upMag = up.Length();
                    float fwdMag = fwd.Length();

                    //reading garbage
                    if (rhtMag < 0.9f || upMag < 0.9f || fwdMag < 0.9f)
                    {
                        IsConnected = false;
                        IsRunning = false;
                        break;
                    }

                    if ( !lastFrameValid)
                    {
                        lastTransform = transform;
                        lastFrameValid = true;
                        lastVelocity = Vector3.Zero;
                        lastYaw = 0.0f;
                        continue;
                    }

                    DIRT5API telemetryData = new DIRT5API();

                    if (dt <= 0)
                        dt = 1.0f;


                    Vector3 worldVelocity = ( transform.Translation - lastTransform.Translation ) / dt;
                    lastTransform = transform;

                    Matrix4x4 rotation = new Matrix4x4();
                    rotation = transform;
                    rotation.M41 = 0.0f;
                    rotation.M42 = 0.0f;
                    rotation.M43 = 0.0f;
                    rotation.M44 = 1.0f;

                    Matrix4x4 rotInv = new Matrix4x4();
                    Matrix4x4.Invert(rotation, out rotInv);
                                       
                    Vector3 localVelocity = Vector3.Transform(worldVelocity, rotInv);

                    telemetryData.velX = worldVelocity.X;
                    telemetryData.velZ = worldVelocity.Z;

                    Vector3 localAcceleration = localVelocity - lastVelocity;
                    lastVelocity = localVelocity;


                    telemetryData.accX = localAcceleration.X * 10.0f;
                    telemetryData.accY = localAcceleration.Y * 100.0f;
                    telemetryData.accZ = localAcceleration.Z * 10.0f;


                    float pitch = (float)Math.Asin(-fwd.Y);
                    float yaw = (float)Math.Atan2(fwd.X, fwd.Z);

                    float roll = 0.0f;
                    Vector3 rhtPlane = rht;
                    rhtPlane.Y = 0;
                    rhtPlane = Vector3.Normalize( rhtPlane );
                    if(rhtPlane.Length() <= float.Epsilon)
                    {
                        roll = -(float)(Math.Sign( rht.Y ) * Math.PI * 0.5f);
//                        Debug.WriteLine( "---Roll = " + roll + " " + Math.Sign( rht.Y ) );
                    }
                    else
                    {
                        roll = -(float)Math.Asin( Vector3.Dot( up, rhtPlane ));
//                        Debug.WriteLine( "Roll = " + roll + " " + Math.Sign(rht.Y) );
                    }
  //                  Debug.WriteLine( "" );

                    telemetryData.pitchPos = pitch;
                    telemetryData.yawPos = yaw;
                    telemetryData.rollPos = roll;

                    telemetryData.yawRate = CalculateAngularChange(lastYaw, yaw) * (180.0f / (float)Math.PI);
                    lastYaw = yaw;

                    // otherwise we are connected
                    IsConnected = true;

                    if(IsConnected)
                    { 
                        IsRunning = true;


                        DIRT5API telemetryToSend = new DIRT5API();
                        telemetryToSend.Reset();

                        telemetryToSend.CopyFields(telemetryData);

                        telemetryToSend.accX = accXSmooth.Filter( telemetryData.accX );
                        telemetryToSend.accY = accYSmooth.Filter( telemetryData.accY );
                        telemetryToSend.accZ = accZSmooth.Filter( telemetryData.accZ );


                        telemetryToSend.pitchPos = pitchFilter.Filter(telemetryData.pitchPos);
                        telemetryToSend.rollPos = rollFilter.Filter(telemetryData.rollPos);
                        telemetryToSend.yawPos = yawFilter.Filter(telemetryData.yawPos);

                        telemetryToSend.velX = velXSmooth.Filter(velXFilter.Filter(telemetryData.velX));
                        telemetryToSend.velZ = velZSmooth.Filter(velZFilter.Filter(telemetryData.velZ));

                        telemetryToSend.yawRate = yawRateSmooth.Filter(yawRateFilter.Filter(telemetryData.yawRate));

                        telemetryToSend.yawAcc = slipAngleSmooth.Filter(telemetryToSend.CalculateSlipAngle());

                        sw.Restart();

                        TelemetryEventArgs args = new TelemetryEventArgs(
                            new DIRT5TelemetryInfo(telemetryToSend, lastTelemetryData));
                        RaiseEvent(OnTelemetryUpdate, args);

                        lastTelemetryData = telemetryToSend;
                        Thread.Sleep(1000/100);
                    }
                    else if (sw.ElapsedMilliseconds > 500)
                    {
                        IsRunning = false;
                    }
                }
                catch (Exception e)
                {
                    LogError("DIRT5TelemetryProvider Exception while processing data", e);
                    mmf.Dispose();
                    IsConnected = false;
                    IsRunning = false;
                    Thread.Sleep(1000);
                }

            }

            mmf.Dispose();
            IsConnected = false;
            IsRunning = false;

        }


        float CalculateAngularChange(float sourceA, float targetA)
        {
            sourceA *= (180.0f / (float)Math.PI);
            targetA *= (180.0f / (float)Math.PI);

            float a = targetA - sourceA;
            a = (a + 180) % 360 - 180;

            return a * ((float)Math.PI / 180.0f);
        }
      

    }


}
