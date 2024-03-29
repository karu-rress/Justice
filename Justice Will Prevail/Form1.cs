﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using Trinet.Core.IO.Ntfs;
using System.Security.Cryptography;

namespace Justice_Will_Prevail
{

    public partial class Form1 : Form
    {
        int totalPass = 0;
        int totalFail = 0;

        string fileName = null;

        public Form1()
        {
            InitializeComponent();
            eventTask = Task.Run(() => GetEventList());
        }

        void WriteLine(string obj) => outputBox.Text += $"{obj}\n";

        private void WriteStep(string str)
        {
            string sz = str;
            if (str[0] is '\n')
            {
                sz = str.Substring(1);
            }
            label1.Text = Text = sz;
            WriteLine(str);
        }

        Task<List<string>> eventTask;

        /// <summary>
        /// Writes the result to the outputBox
        /// </summary>
        /// <param name="passed">Whether the test has passed.</param>
        /// <param name="msg">to print to the box.</param>
        /// <returns>passed</returns>
        private bool ShowResult(bool passed, string msg)
        {
            WriteLine($"[[{(passed ? "PASS" : "FAIL")}]] {msg} {(passed ? "PASSED" : "FAILED")}.");

            if (passed)
                totalPass++;
            else
            {
                totalFail++;
            }
            return passed;
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private async void button1_Click(object sender, EventArgs e)
        {
            Reset();
            MessageBox.Show("지오지브라 파일을 선택하세요.\n(파일 이름: 2901고국고.ggb)", "Choose file...", MessageBoxButtons.OK, MessageBoxIcon.Information);

            using OpenFileDialog openFileDialog1 = new()
            {
                Filter = "geogebra files (*.ggb)|*.ggb|All files (*.*)|*.*",
                FilterIndex = 1
            };
            if (openFileDialog1.ShowDialog() is DialogResult.OK) // Test result.
            {
                fileName = openFileDialog1.FileName;
                if (fileName.Contains(".ggb") is false)
                {
                    MessageBox.Show("지오지브라 파일이 아닙니다. 다시 선택하세요.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    fileName = null;
                    return;
                }
                // Regex reg = new Regex(@"2\d{3}\s*...\.ggb");
                nextButton.Enabled = false;
                await Run();
            }
        }

        private async Task Run()
        {
            // var sha256 = SHA256CheckSumAsync(fileName);

            WriteLine("===================START===================");

            var (create, modify) = GetFileTime();
            WriteStep("(Step 1) Detecting the file creation / modification time...");
            WriteLine($"File name: {fileName}");
            WriteLine($"Created: {create:yyyy-MM-dd HH:mm:ss}");
            WriteLine($"Modified: {modify:yyyy-MM-dd HH:mm:ss}");
            ShowResult(create < new DateTime(2021, 05, 14), "Checking whether the file was created in proper day");

            WriteStep("\n(Step 2) Calculating the file time...");
            ShowResult(modify >= create, "Ensuring modification is later than creation");

            TimeSpan gap = modify - create;
            ShowResult(gap.TotalSeconds is >= 2 and <= 15, "Checking the time gap of the file");

            
            var fileInfo = new FileInfo(fileName);
            using (StreamReader alternateStream = new(fileInfo.GetAlternateDataStream("Zone.Identifier").OpenRead()))
            {
                var adsTask = alternateStream.ReadToEndAsync();
                WriteStep("\n(Step 3) Checking the file is from geogebra...");
                string buf = await adsTask;
                ShowResult(buf.Contains(@"HostUrl=about:internet"), "Checking the file is from geogebra");

            }
            

            progressBar1.Value = 20;
            WriteStep("\n(Step 4) Detecting if system clock has been changed...");
            var changeLogs = await eventTask; // GetEventList()
            if (!ShowResult(changeLogs.Any() is false, "System clock changing detection"))
            {
                changeLogs.ForEach(x => WriteLine(x));
            }


            progressBar1.Value = 100;
            if (totalFail is 1)
            {
                progressBar1.SetState(3);
            }
            else if (totalFail is >= 2)
            {
                progressBar1.SetState(2);
            }
            WriteLine("==================== END ====================");
            WriteLine($"Total Passed: {totalPass}/{totalPass + totalFail}");
            WriteLine($"Total Failed: {totalFail}/{totalPass + totalFail}");
            WriteStep((totalFail is 0) ? $"The file {fileName} is verified." : $"The file {fileName} couldn't be verified.");
            // WriteLine($"SHA-256 checksum: {await sha256}");
            MessageBox.Show("이 창의 모든 내용이 나오도록 캡처해서 클래스룸에 제출하세요.", "Finished", MessageBoxButtons.OK, MessageBoxIcon.Information);
            nextButton.Text = "Restart";
            nextButton.Enabled = true;
        }

        public Task<string> SHA256CheckSumAsync(string filePath)
            => Task<string>.Factory.StartNew(() =>
            {
                using var sha256 = SHA256.Create();
                using var fileStream = File.OpenRead(filePath);
                return Convert.ToBase64String(sha256.ComputeHash(fileStream));
            });

        private void Reset()
        {
            progressBar1.SetState(1);
            progressBar1.Value = 0;
            outputBox.Text = string.Empty;
            nextButton.Text = "Start";
            totalPass = totalFail = 0;
        }

        private List<string> GetEventList()
        {
            EventLog log = new("Security");
            Regex rx = new(@"([01]\dT\d{2}):(\d{2}):\d{2}\.\d*Z");
            string[] splitArray;
            string ptimeStr, ntimeStr, ptimeGroup, ntimeGroup;
            Match ptimeMatch, ntimeMatch;

            MatchCollection collection;

            return log.Entries.Cast<EventLogEntry>()
                .Where(x => {
                    bool retval = x.InstanceId is 4616;
                    retval &= x.TimeGenerated >= new DateTime(2021, 5, 2);
                    retval &= x.TimeGenerated <= new DateTime(2021, 5, 14);
                    return retval;
                })
                .Where(x =>
                {
                    // New Time:\t\t2021-05-06T10:04:57.4319657Z
                    splitArray = x.Message.Split(new[] { '\r', '\n' });
                    (ptimeStr, ntimeStr) = (splitArray[24], splitArray[26]);
                    (ptimeMatch, ntimeMatch) = (rx.Match(ptimeStr), rx.Match(ntimeStr));
                    (ptimeGroup, ntimeGroup) = (ptimeMatch.Groups[2].Value, ntimeMatch.Groups[2].Value);

                    return !(ptimeMatch.Groups[1].Value == ntimeMatch.Groups[1].Value &&
                            Convert.ToInt32(ntimeGroup) - Convert.ToInt32(ptimeGroup) <= 2);
                })
                .Select(x => {
                    collection = new Regex(@"2021\-05\-1\dT\d{2}:(\d{2}):\d{2}\.\d*Z").Matches(x.Message);
                    return collection[0].Value + " seems to be changed to " + collection[1].Value;
                }).ToList();
        }

        private (DateTime creation, DateTime modification) GetFileTime()
        {
            DateTime creation = File.GetCreationTime(fileName);
            DateTime modification = File.GetLastWriteTime(fileName);

            return (creation, modification);
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }
    }

    public static class ModifyProgressBarColor
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr w, IntPtr l);
        public static void SetState(this ProgressBar pBar, int state)
        {
            SendMessage(pBar.Handle, 1040, (IntPtr)state, IntPtr.Zero);
        }
    }
}
