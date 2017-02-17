// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.IO.PortsTests;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Legacy.Support;
using Xunit;
using ThreadState = System.Threading.ThreadState;

namespace System.IO.Ports.Tests
{
    public class SerialStream_WriteByte_Generic : PortsTest
    {
        // Set bounds fore random timeout values.
        // If the min is to low write will not timeout accurately and the testcase will fail
        private static int minRandomTimeout = 250;

        // If the max is to large then the testcase will take forever to run
        private static int maxRandomTimeout = 2000;

        // If the percentage difference between the expected timeout and the actual timeout
        // found through Stopwatch is greater then 10% then the timeout value was not correctly
        // to the write method and the testcase fails.
        private static double maxPercentageDifference = .15;
        private static readonly int NUM_TRYS = 5;
        private static readonly byte DEFAULT_BYTE = 0;

        #region Test Cases

        [ConditionalFact(nameof(HasOneSerialPort))]
        public void WriteAfterClose()
        {
            using (SerialPort com = new SerialPort(TCSupport.LocalMachineSerialInfo.FirstAvailablePortName))
            {
                Debug.WriteLine("Verifying write method throws exception after a call to Cloes()");

                com.Open();
                Stream serialStream = com.BaseStream;
                com.Close();

                VerifyWriteException(serialStream, typeof(ObjectDisposedException));
            }
        }


        [ConditionalFact(nameof(HasOneSerialPort))]
        public void WriteAfterBaseStreamClose()
        {
            using (SerialPort com = new SerialPort(TCSupport.LocalMachineSerialInfo.FirstAvailablePortName))
            {
                Debug.WriteLine("Verifying write method throws exception after a call to BaseStream.Close()");

                com.Open();
                Stream serialStream = com.BaseStream;
                com.BaseStream.Close();

                VerifyWriteException(serialStream, typeof(ObjectDisposedException));
            }
        }

        [ConditionalFact(nameof(HasNullModem))]
        public void Timeout()
        {
            using (var com1 = new SerialPort(TCSupport.LocalMachineSerialInfo.FirstAvailablePortName))
            using (var com2 = new SerialPort(TCSupport.LocalMachineSerialInfo.SecondAvailablePortName))
            {
                var rndGen = new Random(-55);

                com1.WriteTimeout = rndGen.Next(minRandomTimeout, maxRandomTimeout);
                com1.Handshake = Handshake.XOnXOff;

                Debug.WriteLine("Verifying WriteTimeout={0}", com1.WriteTimeout);

                com1.Open();
                com2.Open();

                com2.BaseStream.WriteByte(19);
                Thread.Sleep(250);
                com2.Close();

                VerifyTimeout(com1);
            }
        }

        [OuterLoop("Slow test")]
        [ConditionalFact(nameof(HasOneSerialPort), nameof(HasHardwareFlowControl))]
        public void SuccessiveReadTimeout()
        {
            using (SerialPort com = new SerialPort(TCSupport.LocalMachineSerialInfo.FirstAvailablePortName))
            {
                var rndGen = new Random(-55);


                com.WriteTimeout = rndGen.Next(minRandomTimeout, maxRandomTimeout);
                com.Handshake = Handshake.RequestToSendXOnXOff;
                // 		com.Encoding = new System.Text.UTF7Encoding();
                com.Encoding = Encoding.Unicode;

                Debug.WriteLine("Verifying WriteTimeout={0} with successive call to write method",
                    com.WriteTimeout);
                com.Open();

                try
                {
                    com.BaseStream.WriteByte(DEFAULT_BYTE);
                }
                catch (TimeoutException)
                {
                }

                VerifyTimeout(com);
            }
        }

        [ConditionalFact(nameof(HasNullModem), nameof(HasHardwareFlowControl))]
        public void SuccessiveReadTimeoutWithWriteSucceeding()
        {
            using (var com1 = new SerialPort(TCSupport.LocalMachineSerialInfo.FirstAvailablePortName))
            {
                var rndGen = new Random(-55);
                var asyncEnableRts = new AsyncEnableRts();
                var t = new Thread(asyncEnableRts.EnableRTS);

                int waitTime;

                com1.WriteTimeout = rndGen.Next(minRandomTimeout, maxRandomTimeout);
                com1.Handshake = Handshake.RequestToSend;
                com1.Encoding = new UTF8Encoding();

                Debug.WriteLine(
                    "Verifying WriteTimeout={0} with successive call to write method with the write succeeding sometime before it's timeout",
                    com1.WriteTimeout);
                com1.Open();

                // Call EnableRTS asynchronously this will enable RTS in the middle of the following write call allowing it to succeed 
                // before the timeout is reached
                t.Start();
                waitTime = 0;

                while (t.ThreadState == ThreadState.Unstarted && waitTime < 2000)
                {
                    // Wait for the thread to start
                    Thread.Sleep(50);
                    waitTime += 50;
                }

                try
                {
                    com1.BaseStream.WriteByte(DEFAULT_BYTE);
                }
                catch (TimeoutException)
                {
                }

                asyncEnableRts.Stop();

                while (t.IsAlive)
                    Thread.Sleep(100);

                VerifyTimeout(com1);

            }
        }

        [ConditionalFact(nameof(HasOneSerialPort), nameof(HasHardwareFlowControl))]
        public void BytesToWrite()
        {
            using (SerialPort com = new SerialPort(TCSupport.LocalMachineSerialInfo.FirstAvailablePortName))
            {
                Debug.WriteLine("Verifying BytesToWrite with one call to Write");

                com.Handshake = Handshake.RequestToSend;
                com.Open();
                com.WriteTimeout = 200;

                // Write a random byte[] asynchronously so we can verify some things while the write call is blocking
                Task task = Task.Run(() => WriteRandomDataBlock(com, TCSupport.MinimumBlockingByteCount));
                TCSupport.WaitForTaskToStart(task);

                TCSupport.WaitForWriteBufferToLoad(com, TCSupport.MinimumBlockingByteCount);

                // Wait for write method to timeout and complete the task
                TCSupport.WaitForTaskCompletion(task);
            }


        }

        [OuterLoop("Slow Test")]
        [ConditionalFact(nameof(HasOneSerialPort), nameof(HasHardwareFlowControl))]
        public void BytesToWriteSuccessive()
        {
            using (SerialPort com = new SerialPort(TCSupport.LocalMachineSerialInfo.FirstAvailablePortName))
            {
                Debug.WriteLine("Verifying BytesToWrite with successive calls to Write");

                com.Handshake = Handshake.RequestToSend;
                com.Open();
                com.WriteTimeout = 4000;

                int blockLength = TCSupport.MinimumBlockingByteCount;

                // Write a random byte[] asynchronously so we can verify some things while the write call is blocking
                Task t1 = Task.Run(() => WriteRandomDataBlock(com, blockLength));

                TCSupport.WaitForTaskToStart(t1);

                TCSupport.WaitForWriteBufferToLoad(com, blockLength);

                // Write a random byte[] asynchronously so we can verify some things while the write call is blocking
                Task t2 = Task.Run(() => WriteRandomDataBlock(com, blockLength));

                TCSupport.WaitForTaskToStart(t2);

                TCSupport.WaitForWriteBufferToLoad(com, blockLength*2);

                // Wait for both write methods to timeout
                TCSupport.WaitForTaskCompletion(t1);
                TCSupport.WaitForTaskCompletion(t2);
            }
        }

        [ConditionalFact(nameof(HasOneSerialPort))]
        public void Handshake_None()
        {
            using (SerialPort com = new SerialPort(TCSupport.LocalMachineSerialInfo.FirstAvailablePortName))
            {
                Debug.WriteLine("Verifying Handshake=None");

                com.Open();

                // Write a random byte[] asynchronously so we can verify some things while the write call is blocking
                Task task = Task.Run(() => WriteRandomDataBlock(com, TCSupport.MinimumBlockingByteCount));

                TCSupport.WaitForTaskToStart(task);

                // Wait for write methods to complete
                TCSupport.WaitForTaskCompletion(task);

                Assert.Equal(0, com.BytesToWrite);
            }
        }

        [ConditionalFact(nameof(HasNullModem), nameof(HasHardwareFlowControl))]
        public void Handshake_RequestToSend()
        {
            Verify_Handshake(Handshake.RequestToSend);
        }

        [ConditionalFact(nameof(HasNullModem))]
        public void Handshake_XOnXOff()
        {
            Verify_Handshake(Handshake.XOnXOff);
        }

        [ConditionalFact(nameof(HasNullModem), nameof(HasHardwareFlowControl))]
        public void Handshake_RequestToSendXOnXOff()
        {
            Verify_Handshake(Handshake.RequestToSendXOnXOff);
        }

        private class AsyncEnableRts
        {
            private bool _stop;


            public void EnableRTS()
            {
                lock (this)
                {
                    using (var com2 = new SerialPort(TCSupport.LocalMachineSerialInfo.SecondAvailablePortName))
                    {
                        var rndGen = new Random(-55);
                        int sleepPeriod = rndGen.Next(minRandomTimeout, maxRandomTimeout / 2);

                        // Sleep some random period with of a maximum duration of half the largest possible timeout value for a write method on COM1
                        Thread.Sleep(sleepPeriod);

                        com2.Open();
                        com2.RtsEnable = true;

                        while (!_stop)
                            Monitor.Wait(this);

                        com2.RtsEnable = false;
                    }
                }
            }


            public void Stop()
            {
                lock (this)
                {
                    _stop = true;
                    Monitor.Pulse(this);
                }
            }
        }


        #endregion

        #region Verification for Test Cases
        private static void VerifyWriteException(Stream serialStream, Type expectedException)
        {
            Assert.Throws(expectedException, () => serialStream.WriteByte(DEFAULT_BYTE));
        }

        private void VerifyTimeout(SerialPort com)
        {
            var timer = new Stopwatch();
            int expectedTime = com.WriteTimeout;
            var actualTime = 0;
            double percentageDifference;

            try
            {
                com.BaseStream.WriteByte(DEFAULT_BYTE); // Warm up write method
            }
            catch (TimeoutException) { }

            Thread.CurrentThread.Priority = ThreadPriority.Highest;

            for (var i = 0; i < NUM_TRYS; i++)
            {
                timer.Start();
                try
                {
                    com.BaseStream.WriteByte(DEFAULT_BYTE);
                }
                catch (TimeoutException)
                {
                }

                timer.Stop();

                actualTime += (int)timer.ElapsedMilliseconds;
                timer.Reset();
            }

            Thread.CurrentThread.Priority = ThreadPriority.Normal;
            actualTime /= NUM_TRYS;
            percentageDifference = Math.Abs((expectedTime - actualTime) / (double)expectedTime);

            // Verify that the percentage difference between the expected and actual timeout is less then maxPercentageDifference
            if (maxPercentageDifference < percentageDifference)
            {
                Fail("ERROR!!!: The write method timed-out in {0} expected {1} percentage difference: {2}", actualTime, expectedTime, percentageDifference);
            }
        }

        private void Verify_Handshake(Handshake handshake)
        {
            using (var com1 = new SerialPort(TCSupport.LocalMachineSerialInfo.FirstAvailablePortName))
            using (var com2 = new SerialPort(TCSupport.LocalMachineSerialInfo.SecondAvailablePortName))
            {
                Debug.WriteLine("Verifying Handshake={0}", handshake);
                com1.Handshake = handshake;
                com1.Open();
                com2.Open();

                // Setup to ensure write will bock with type of handshake method being used
                if (Handshake.RequestToSend == handshake || Handshake.RequestToSendXOnXOff == handshake)
                {
                    com2.RtsEnable = false;
                }

                if (Handshake.XOnXOff == handshake || Handshake.RequestToSendXOnXOff == handshake)
                {
                    com2.BaseStream.WriteByte(19);
                    Thread.Sleep(250);
                }

                // Write a block of random data asynchronously so we can verify some things while the write call is blocking
                Task task = Task.Run(() => WriteRandomDataBlock(com1, TCSupport.MinimumBlockingByteCount));

                TCSupport.WaitForTaskToStart(task);

                TCSupport.WaitForWriteBufferToLoad(com1, TCSupport.MinimumBlockingByteCount);

                // Verify that CtsHolding is false if the RequestToSend or RequestToSendXOnXOff handshake method is used
                if ((Handshake.RequestToSend == handshake || Handshake.RequestToSendXOnXOff == handshake) &&
                    com1.CtsHolding)
                {
                    Fail("ERROR!!! Expected CtsHolding={0} actual {1}", false, com1.CtsHolding);
                }

                // Setup to ensure write will succeed
                if (Handshake.RequestToSend == handshake || Handshake.RequestToSendXOnXOff == handshake)
                {
                    com2.RtsEnable = true;
                }

                if (Handshake.XOnXOff == handshake || Handshake.RequestToSendXOnXOff == handshake)
                {
                    com2.BaseStream.WriteByte(17);
                }

                // Wait till write finishes
                TCSupport.WaitForTaskCompletion(task);

                // Verify that the correct number of bytes are in the buffer
                // (There should be nothing because it's all been transmitted after the flow control was released)
                Assert.Equal(0, com1.BytesToWrite);

                // Verify that CtsHolding is true if the RequestToSend or RequestToSendXOnXOff handshake method is used
                if ((Handshake.RequestToSend == handshake || Handshake.RequestToSendXOnXOff == handshake) &&
                    !com1.CtsHolding)
                {
                    Fail("ERROR!!! Expected CtsHolding={0} actual {1}", true, com1.CtsHolding);
                }
            }
        }


        private static void WriteRandomDataBlock(SerialPort com, int blockLength)
        {
            var rndGen = new Random(-55);
            byte[] randomData = new byte[blockLength];
            rndGen.NextBytes(randomData);

            try
            {
                com.BaseStream.Write(randomData, 0, randomData.Length);
            }
            catch (TimeoutException)
            {
            }
        }

        #endregion
    }
}