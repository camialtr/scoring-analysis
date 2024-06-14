﻿#if (DEBUGX86 || RELEASEX86)
using JDNow;
#elif (DEBUGX64 || RELEASEX64)
using MoveSpaceWrapper;
#endif
using Newtonsoft.Json;
using System.Diagnostics;
using System.ComponentModel;
using NativeFileDialogSharp;

namespace scoring_analysis
{
    internal unsafe class Program : Base
    {
        static void Main()
        {
            InitialLogic();
        }
        static void NotImplemented()
        {
            console = "This function is not yet implemented, choose another!";
            InitialLogic();
        }

        static void InitialLogic()
        {
            Console.Clear();
            Console.WriteLine(header);
            Console.WriteLine($"Select an option below: {newLine}");
            foreach (string command in commands) Console.WriteLine(command);
            Console.Write($"{newLine}Type code: ");
            Console.Write($"{newLine}{newLine}[Console]");
            Console.Write($"{newLine}{newLine}{DateTime.Now.ToString("hh:mm:ss")} - {console}");
            Console.SetCursorPosition(11, 7 + commands.Length);
            string? stringTyped = Console.ReadLine();
            switch (stringTyped)
            {
                default:
                    console = "Invalid option, try again!";
                    InitialLogic();
                    break;
                case "0":
                    SwitchAPI();
                    break;
                case "1":
                    ProcessRecordedData();
                    break;
                case "2":
                    NotImplemented();
                    break;
            }
        }

#if (DEBUGX86 || DEBUGX64)
        static void SwitchAPI()
        {
            try
            {
                string exePath = Path.Combine(Environment.CurrentDirectory, "scoring-analysis.exe");
                if (preset == "JDNOW API")
                {
                    Process.Start(exePath.Replace("x86", "x64"));
                }
                else
                {
                    Process.Start(exePath.Replace("x64", "x86"));
                }
            }
            catch (Win32Exception)
            {
                console = "Unable to open x86 version of this project, verify your files integrity!";
                InitialLogic();
            }            
        }
#elif (RELEASEX86 || RELEASEX64)
        static void SwitchAPI()
        {
            try
            {
                string exePath = Path.Combine(Environment.CurrentDirectory, "Assemblies", "scoring-analysis.exe");
                if (preset == "JDNOW API")
                {
                    Process.Start(exePath.Replace(@"Assemblies\", ""));
                }
                else
                {
                    Process.Start(exePath);
                }
            }
            catch (Win32Exception)
            {
                console = "Unable to open x64 version of this project, verify your files integrity!";
                InitialLogic();
            }            
        }
#endif
#if (DEBUGX86 || RELEASEX86)
        static void ProcessRecordedData()
        {
            WriteStaticHeader(true, "Select a file...");
            DialogResult dialogResult = Dialog.FileOpen("json", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            if (dialogResult.IsCancelled) { console = "Operation cancelled..."; InitialLogic(); }
            ScoringRecorder recordedData = JsonConvert.DeserializeObject<ScoringRecorder>(File.ReadAllText(dialogResult.Path));
            WriteStaticHeader(true, "Verifying file...");
            if (string.IsNullOrEmpty(recordedData.mapName) || recordedData.moves == null || recordedData.recordedAccData == null || recordedData.recordedScore == null)
            {
                console = "Error: Seems like you have selected an incorrect file, verify your file structure or select a valid one!";
                InitialLogic();
            }
            else
            {
                GenerateJDNEXTJSON(recordedData);
                GenerateJDNOWJSON(recordedData);
                console = "Successfully created JDNEXT-JSON and JDNOW-JSON on comparatives directory!";
                InitialLogic();
            }
        }        

        static void GenerateJDNEXTJSON(ScoringRecorder recordedData)
        {
            WriteStaticHeader(false, "Generating JDNEXT-JSON...");
            ComparativeJSON jdnextJSON = new()
            {
                mapName = recordedData.mapName,
                comparativeType = ComparativeType.JDNEXT,
                values = recordedData.recordedScore
            };
            File.WriteAllText(Path.Combine(GetOrCreateComparativesDirectory(recordedData), "jdnext.json"), JsonConvert.SerializeObject(jdnextJSON, Formatting.Indented));
        }        

        static void GenerateJDNOWJSON(ScoringRecorder recordedData)
        {
            WriteStaticHeader(true, "Initializing JDNOW score api...");
            Scoring scoring = new();
            Move lastMove = recordedData.moves.Last();
            int classifiersSuccessCount = 0;
            int classifiersFailureCount = 0;
            foreach (Move move in recordedData.moves)
            {
                bool classifierLoaded = scoring.LoadClassifier(move.data, Convert.FromBase64String(move.data));
                bool moveLoaded = scoring.LoadMove(move.data, (int)(move.time * 1000), (int)(move.duration * 1000), Convert.ToBoolean(move.goldMove), move.Equals(lastMove));
                if (classifierLoaded && moveLoaded) { classifiersSuccessCount++; } else { classifiersFailureCount++; }
            }
            if (classifiersFailureCount != 0)
            {
                console = $"Error: At least one classifier failed to load!";
                InitialLogic();
            }
            else
            {
                Console.Clear();
                Console.WriteLine("Scoring...");
                List<RecordedScore> recordedValues = new();
                int moveID = 0; float lastScore = 0f;
                foreach (RecordedAccData accData in recordedData.recordedAccData)
                {
                    ScoreResult scoreResult = scoring.GetLastScore();
                    (int, float) scoreData = GetScoreData(scoreResult, moveID, lastScore, recordedValues);
                    moveID = scoreData.Item1; lastScore = scoreData.Item2;
                    scoring.AddSample(accData.accX, accData.accY, accData.accZ, accData.mapTime - 0.1f);
                }
                ComparativeJSON jdnowJSON = new()
                {
                    mapName = recordedData.mapName,
                    comparativeType = ComparativeType.JDNOW,
                    values = recordedValues
                };
                File.WriteAllText(Path.Combine(GetOrCreateComparativesDirectory(recordedData), "jdnow.json"), JsonConvert.SerializeObject(jdnowJSON, Formatting.Indented));
            }
        }

        static (int, float) GetScoreData(ScoreResult scoreResult, int moveID, float lastScore, List<RecordedScore> recordedValues)
        {            
            if (scoreResult.moveNum == moveID)
            {
                string feedback = string.Empty;
                switch (scoreResult.rating)
                {
                    case 0:
                        if (scoreResult.isGoldMove)
                        {
                            feedback = "MISSYEAH";
                        }
                        else
                        {
                            feedback = "MISS";
                        }
                        break;
                    case 1:
                        feedback = "OK";
                        break;
                    case 2:
                        feedback = "GOOD";
                        break;
                    case 3:
                        feedback = "PERFECT";
                        break;
                    case 4:
                        feedback = "YEAH";
                        break;
                }
                recordedValues.Add(new() { feedback = feedback, addedScore = scoreResult.totalScore - lastScore, totalScore = scoreResult.totalScore});
                moveID++; lastScore = scoreResult.totalScore;
            }
            return (moveID, lastScore);
        }
#elif (DEBUGX64 || RELEASEX64)
        static void ProcessRecordedData()
        {
            WriteStaticHeader(true, "Select a file...");
            DialogResult dialogResult = Dialog.FileOpen("json", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            if (dialogResult.IsCancelled) { console = "Operation cancelled..."; InitialLogic(); }
            ScoringRecorder recordedData = JsonConvert.DeserializeObject<ScoringRecorder>(File.ReadAllText(dialogResult.Path));
            WriteStaticHeader(true, "Verifying file...");
            if (string.IsNullOrEmpty(recordedData.mapName) || recordedData.moves == null || recordedData.recordedAccData == null || recordedData.recordedScore == null)
            {
                console = "Error: Seems like you have selected an incorrect file, verify your file structure or select a valid one!";
                InitialLogic();
            }
            else
            {
                GenerateUAFJSON(recordedData);
                console = "Successfully created UAF-JSON on comparatives directory!";
                InitialLogic();
            }
        }

        static void GenerateUAFJSON(ScoringRecorder recordedData)
        {
            WriteStaticHeader(true, "Initializing UAF score api...");
            ScoreManager scoreManager = new();
            scoreManager.Init();
            List<RecordedScore> recordedValues = new();
            float totalScore = 13333f;
            float goldScore = 1000f;
            float moveScore = totalScore - goldScore;
            int goldCount = 0;
            int moveCount = 0;
            foreach (Move move in recordedData.moves) 
            {
                if (move.goldMove == 1)
                { 
                    goldCount++; 
                } 
                else 
                { 
                    moveCount++; 
                } 
            }
            goldScore = goldScore / goldCount;
            moveScore = moveScore / moveCount;
            float finalScore = 0f;
            foreach (Move move in recordedData.moves)
            {
                byte[] moveData = Convert.FromBase64String(move.data);
                fixed (byte* movePointer = &moveData[0])
                {
                    scoreManager.StartMoveAnalysis(movePointer, (uint)moveData.Length, move.duration);
                    foreach (RecordedAccData accData in recordedData.recordedAccData)
                    {
                        if (accData.mapTime >= move.time && accData.mapTime <= (move.time + move.duration))
                        {
                            float time = InverseLerp(accData.mapTime - 0.1f, move.time, move.time + move.duration);
                            scoreManager.bUpdateFromProgressRatioAndAccels(time, accData.accX, accData.accY, accData.accZ);
                        }
                    }
                    scoreManager.StopMoveAnalysis();
                }
                if (move.goldMove == 1) 
                {
                    float percentage = 0f;
                    for (int i = 1; i < 20; i++)
                    {
                        float tempPercentage = scoreManager.GetSignalValue((byte)i);
                        if (tempPercentage.ToString() != "4,2949673E+09" && tempPercentage > 0) percentage += tempPercentage;
                    }
                    percentage = percentage / 100;
                    if (percentage > 6) percentage = 6;
                    float score = Single.Lerp(0, moveScore, percentage);
                    finalScore += score;
                    recordedValues.Add(new() { feedback = "UNKNOW", addedScore = score, totalScore = finalScore });
                    Console.WriteLine($"GOLD: {percentage} | Tedency: {scoreManager.GetLastMoveDirectionImpactFactor().ToString()}");
                } 
                else
                {
                    float percentage = 0f;
                    for (int i = 1; i < 20; i++)
                    {
                        float tempPercentage = scoreManager.GetSignalValue((byte)i);
                        if (tempPercentage.ToString() != "4,2949673E+09" && tempPercentage > 0) percentage += tempPercentage;
                    }
                    percentage = percentage / 100;
                    if (percentage > 6) percentage = 6;
                    float score = Single.Lerp(0, moveScore, percentage);
                    finalScore += score;
                    recordedValues.Add(new() { feedback = "UNKNOW", addedScore = score, totalScore = finalScore });
                    Console.WriteLine($"MOVE: {percentage} | Tedency: {scoreManager.GetLastMoveDirectionImpactFactor().ToString()}");
                }
            }
            Console.WriteLine($"Final Score: {finalScore}");
            ComparativeJSON uafJSON = new()
            {
                mapName = recordedData.mapName,
                comparativeType = ComparativeType.UAF,
                values = recordedValues
            };
            File.WriteAllText(Path.Combine(GetOrCreateComparativesDirectory(recordedData), "uaf.json"), JsonConvert.SerializeObject(uafJSON, Formatting.Indented));
        }

        public static float InverseLerp(float value, float a, float b)
        {
            if (a == b) { return 0f; }
            return (value - a) / (b - a);
        }
#endif
        static void WriteStaticHeader(bool sleep, string log)
        {
            Console.Clear();
            Console.WriteLine(header);
            Console.WriteLine($"{commands[1].Replace("[1] ", "")}{newLine}");
            Console.Write($"[Console]");
            console = log;
            Console.Write($"{newLine}{newLine}{DateTime.Now.ToString("hh:mm:ss")} - {console}");
            if (sleep) Thread.Sleep(500);
        }

        static string GetOrCreateComparativesDirectory(ScoringRecorder recordedData)
        {
            string comparativesDirectory = Path.Combine(Environment.CurrentDirectory, "Comparatives").Replace(@"Assemblies\", "");
            if (!Directory.Exists(comparativesDirectory)) Directory.CreateDirectory(comparativesDirectory);
            string mapComparativesDirectory = Path.Combine(comparativesDirectory, recordedData.mapName);
            if (!Directory.Exists(mapComparativesDirectory)) Directory.CreateDirectory(mapComparativesDirectory);
            return mapComparativesDirectory;
        }
    }
}
