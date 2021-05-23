using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Runtime.InteropServices;

namespace JusticeWillPrevail
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
            label1.Text = Text = str;
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

        private async void nextButton_Click(object sender, EventArgs e)
        {
            // 초기화면인 경우
            if (fileName is null)
            {
                MessageBox.Show("지오지브라 파일을 선택하세요.\n(파일 이름: 2901고국고.ggb)", "Choose file...", MessageBoxButtons.OK, MessageBoxIcon.Information);

                OpenFileDialog openFileDialog1 = new OpenFileDialog();
                DialogResult result = openFileDialog1.ShowDialog(); // Show the dialog.
                if (result == DialogResult.OK) // Test result.
                {
                    fileName = openFileDialog1.FileName;
                    if (fileName.Contains(".ggb") is false)
                        MessageBox.Show("지오지브라 파일이 아닙니다. 다시 선택하세요.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    // Regex reg = new Regex(@"2\d{3}\s*...\.ggb");
                    nextButton.Enabled = false;
                    await Run();
                }
            }
        }

        private async Task Run()
        {
            WriteLine("===================START===================");

            var (create, modify) = GetFileTime();
            WriteStep("(Step 1) Detecting the file creation / modification time...");
            WriteLine($"File name: {fileName}");
            WriteLine($"Created: {create:yyyy-MM-dd HH:mm:ss}");
            WriteLine($"Modified: {modify:yyyy-MM-dd HH:mm:ss}");
            ShowResult(create < new DateTime(2021, 05, 14), "Checking whether the file was created in proper day");

            WriteStep("(Step 2) Calculating the file time...");
            ShowResult(modify >= create, "Ensuring modification is later than creation");

            TimeSpan gap = modify - create;
            ShowResult(gap.TotalSeconds is >= 3 and <= 15, "Checking the time gap of the file");

            var adsTask = File.ReadAllTextAsync(fileName + ":Zone.Identifier");
            WriteStep("(Step 3) Checking the file is from geogebra...");
            string buf = await adsTask;
            ShowResult(buf.Contains(@"HostUrl=about:internet"), "Checking the file is from geogebra");

            progressBar1.Value = 20;
            WriteStep("(Step 4) Detecting if system clock has been changed...");
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

            if (totalFail is 0)
            {
                WriteStep($"The file {fileName} is verified.");
            }
            else
            {
                WriteStep($"The file {fileName} couldn't be verified.\n");
            }

            MessageBox.Show("이 창의 모든 내용이 나오도록 캡처해서 클래스룸에 제출하세요.", "Finished", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private List<string> GetEventList()
        {
            EventLog log = new EventLog("Security");
            Regex rx = new Regex(@"(1\dT\d{2}):(\d{2}):\d{2}\.\d*Z");
            string[] splitArray;
            string ptimeStr, ntimeStr, ptimeGroup, ntimeGroup;
            Match ptimeMatch, ntimeMatch;

            MatchCollection collection;

            return log.Entries.Cast<EventLogEntry>()
                .Where(x => {
                    bool retval = x.InstanceId is 4616;
                    retval &= x.TimeGenerated >= new DateTime(2021, 5, 12);
                    retval &= x.TimeGenerated <= new DateTime(2021, 5, 14);
                    return retval;
                })
                .Where(x =>
                {
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
