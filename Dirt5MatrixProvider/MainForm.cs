using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;
using Sojaner.MemoryScanner;
using System.Numerics;
using System.IO;
using System.IO.MemoryMappedFiles;


namespace Dirt5MatrixProvider
{

    public delegate void ButtonClickedCallback(object sender, EventArgs e);

    public partial class MainForm : Form
    {
        private delegate void SafeCallProgressDelegate(int progress);
        private delegate void SafeCallBoolDelegate(bool value);
        private delegate void SafeCallStringDelegate(string value);
        Int64 memoryAddress;
        Thread t;
        Process mainProcess = null;
        string saveFilename = "Dirt5Config.txt";


        public ButtonClickedCallback onBtnClicked;

        public MainForm()
        {
            InitializeComponent();

            onBtnClicked = ScanButtonClicked;
            statusLabel.Text = "Select Vehicle & Click Initialize!";

            LoadConfig();
        }

        private void initializeButton_Click(object sender, EventArgs e)
        {
            onBtnClicked(sender, e);
        }



        public void ScanButtonClicked(object sender, EventArgs e)
        {
            Process[] processes = Process.GetProcesses();

            foreach (Process process in processes)
            {
                if (process.ProcessName.Contains("DIRT5"))
                    mainProcess = process;
            }

            if (mainProcess == null) //no processes, better stop
            {
                statusLabel.Text = "DIRT5 exe not running!";
                return;
            }



            initializeButton.Enabled = false;
            statusLabel.Text = "Please Wait";
            progressBar1.Value = 0;


            RegularMemoryScan scan = new RegularMemoryScan(mainProcess, 0, 34359720776);// 140737488355327); //32gig
            scan.ScanProgressChanged += new RegularMemoryScan.ScanProgressedEventHandler(scan_ScanProgressChanged);
            scan.ScanCompleted += new RegularMemoryScan.ScanCompletedEventHandler(scan_ScanCompleted);
            scan.ScanCanceled += new RegularMemoryScan.ScanCanceledEventHandler(scan_ScanCanceled);

            string vehicleString = vehicleSelector.Text;

//            string scanString = "(\0\0\0\0skoda_fabia_r5";
            string scanString = "(\0\0\0\0" + vehicleString;
            scan.StartScanForString(scanString);
        }



        void scan_ScanCanceled(object sender, ScanCanceledEventArgs e)
        {
            initializeButton.Enabled = true;
        }

        void scan_ScanCompleted(object sender, ScanCompletedEventArgs e)
        {
            EnableButtonThreadSafe(initializeButton, true);

            if (e.MemoryAddresses == null || e.MemoryAddresses.Length == 0)
            {
                SetTextBoxThreadSafe(statusLabel, "Failed!");
                return;
            }

            memoryAddress = e.MemoryAddresses[0] + 541; //offset from found address to start of matrix

            SetTextBoxThreadSafe(statusLabel, "Success");

            t = new Thread(Run);
            t.Start();
        }

        void SetTextBoxThreadSafe(TextBox textBox, string text)
        {
            if (textBox.InvokeRequired)
            {
                SafeCallStringDelegate d = new SafeCallStringDelegate((x) => { textBox.Text = x; });
                textBox.Invoke(d, new object[] { text });
            }
            else
                textBox.Enabled = true;
        }
        void SetRichTextBoxThreadSafe(RichTextBox textBox, string text)
        {
            if (textBox.InvokeRequired)
            {
                SafeCallStringDelegate d = new SafeCallStringDelegate((x) => { textBox.Text = x; });
                textBox.Invoke(d, new object[] { text });
            }
            else
                textBox.Enabled = true;
        }

        void EnableButtonThreadSafe(Button button, bool value)
        {
            if (button.InvokeRequired)
            {
                SafeCallBoolDelegate d = new SafeCallBoolDelegate((x) => { button.Enabled = x; });
                button.Invoke(d, new object[] { value });
            }
            else
                button.Enabled = true;

        }


        private void Run()
        {
            bool isStopped = false;
            ProcessMemoryReader reader = new ProcessMemoryReader();

            reader.ReadProcess = mainProcess;
            UInt64 readSize = 4 * 4 * 4;
            byte[] readBuffer = new byte[readSize];
            reader.OpenProcess();

            Mutex mutex = new Mutex(false, "Dirt5MatrixProviderMutex");


            using (MemoryMappedFile mmf = MemoryMappedFile.CreateNew("Dirt5MatrixProvider", 10000))
            {

                while (!isStopped)
                {
                    try
                    {

                        Int64 byteReadSize;
                        reader.ReadProcessMemory((IntPtr)memoryAddress, readSize, out byteReadSize, readBuffer);

                        if (byteReadSize == 0)
                        {
                            continue;
                        }

                        float[] floats = new float[4 * 4];

                        Buffer.BlockCopy(readBuffer, 0, floats, 0, readBuffer.Length);

                        SetRichTextBoxThreadSafe(matrixBox, "" + floats[0] + " " + floats[1] + " " + floats[2] + " " + floats[3] + "\n" + floats[4] + " " + floats[5] + " " + floats[6] + " " + floats[7] + "\n" + floats[8] + " " + floats[9] + " " + floats[10] + " " + floats[11] + "\n" + floats[12] + " " + floats[13] + " " + floats[14] + " " + floats[15]);

                        mutex.WaitOne();

                        using (MemoryMappedViewStream stream = mmf.CreateViewStream())
                        {
                            BinaryWriter writer = new BinaryWriter(stream);
                            writer.Write(readBuffer);
                        }
                        mutex.ReleaseMutex();

                        Thread.Sleep(1000 / 100);
                    }
                    catch (Exception e)
                    {
                        Thread.Sleep(1000);
                    }

                }
            }
        }


        void scan_ScanProgressChanged(object sender, ScanProgressChangedEventArgs e)
        {
            if (progressBar1.InvokeRequired)
            {
                SafeCallProgressDelegate d = new SafeCallProgressDelegate((x) => { progressBar1.Value = x; });
                int value = e.Progress;
                progressBar1.Invoke(d, new object[] { value });
            }
            else
                progressBar1.Value = e.Progress;
        }

        void LoadConfig()
        {
            string[] vehicles = System.IO.File.ReadAllLines("Dirt5Vehicles.txt");

            vehicleSelector.Items.AddRange(vehicles);

            if (File.Exists(saveFilename))
            {
                string[] saveData = System.IO.File.ReadAllLines(saveFilename);

                if (saveData != null && saveData.Length != 0)
                {
                    for (int i = 0; i < vehicles.Length; ++i)
                    {
                        if (vehicles[i] == saveData[0])
                        {
                            vehicleSelector.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
        }

        void SaveConfig()
        {
            string[] saveData = { (string)vehicleSelector.SelectedItem };

            File.WriteAllLines(saveFilename, saveData);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Application.Exit();
        }
         
        private void StatusBox_TextChanged(object sender, EventArgs e)
        {

        }

        private void progressBar1_Click(object sender, EventArgs e)
        {

        }

        private void vehicleSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            SaveConfig();
        }
    }
}
