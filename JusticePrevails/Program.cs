using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace JusticePrevails
{
    class Program
    {
        static int totalPass = 0;
        static int totalFail = 0;

        static bool ShowResult(bool passed, string msg)
        {
            Console.WriteLine($"[[{(passed ? "PASS" : "FAIL")}]] {msg} {(passed?"PASSED":"FAILED")}.");
            if (passed)
                totalPass++;
            else
                totalFail++;

            return passed;
        }
        
        static async Task Main(string[] args)
        {
            var getTask1 = Task.Run(() => GetEventList());

            foreach (char ch in "===================START===================\n")
            {
                Console.Write(ch);
                Thread.Sleep(10);
            }
            Thread.Sleep(100);
            Console.WriteLine("\n(Step 1) Detecting if system clock has been changed...\n");
            Thread.Sleep(400);
            /*
             *  STEP 1.
             *  시스템 시각 위변조 여부 확인
             * 
             * 수학 수행 시간과 관련이 있을 경우 부정행위일 확률이 높음
             * 
             */
            

            var entry = await getTask1;
            if (!ShowResult(entry.Any() is false, "System clock changing detection"))
            {
                entry.ForEach(x => Console.WriteLine(x));
            }

            string filename;
            try
            {
                var getTask2 = Task.Run(() => GetFileTime());

                Thread.Sleep(100);
                Console.WriteLine("\n(Step 2) Detecting the file creation / modification time...\n");
                Thread.Sleep(400);

                var file = await getTask2;
                filename = file.name;

                Console.WriteLine($"Founded file: {file.name}\n");
                Console.WriteLine($"Created: {file.creation:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"Modified: {file.modification:yyyy-MM-dd HH:mm:ss}");

                Console.WriteLine("\n(Step 3) Calculating the file time...\n");
                if (ShowResult(file.modification >= file.creation, "Ensuring modification is later than creation"))
                {
                    TimeSpan gap = file.modification - file.creation;
                    ShowResult(gap.TotalSeconds is >= 3 and <= 15, "Checking the time gap of the file");

                    var getTask3 = File.ReadAllTextAsync(file.name + ":Zone.Identifier");
                    Console.WriteLine("\n(Step 4) Checking the file is from geogebra... (step 2/2)\n");
                    string buf = await getTask3;

                    ShowResult(buf.Contains(@"HostUrl=about:internet"), "File is from geogebra");
                }
            }
            catch
            {
                Console.WriteLine("=====[[ERROR]] EXCEPTION OCCURED: FILE NOT FOUND!!!=====");
                Console.WriteLine("Aborting...");
                return;
            }

            Console.WriteLine("\n==================== END ====================");
            Console.WriteLine($"Total Passed: {totalPass}");
            Console.WriteLine($"Total Failed: {totalFail}");

            Console.WriteLine($"The file {filename} " + ((totalFail is 0) ? "is verified." : "couldn't verified.\n"));
            Console.WriteLine("Press Enter key to exit.");
            Console.ReadLine();

        }

        static (string name, DateTime creation, DateTime modification) GetFileTime()
        {
            Regex reg = new(@"2\d{3}\s*...\.ggb");
                //new Regex(@"from.*\.ggb");
            var files = Directory.GetFiles(@"D:\Downloads\Test Folder\", "*.ggb").Where(path => reg.IsMatch(path)).ToList();
            DateTime creation = File.GetCreationTime(files[0]);
            DateTime modification = File.GetLastWriteTime(files[0]);

            return (files[0], creation, modification);
        }

        static List<string> GetEventList()
        {
            EventLog log = new("Security");
            Regex rx = new(@"(1\dT\d{2}):(\d{2}):\d{2}\.\d*Z");
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
                             }
                         ).ToList();
            //2021-05-17T06:15:02.4093533Z
        }
    }
}


