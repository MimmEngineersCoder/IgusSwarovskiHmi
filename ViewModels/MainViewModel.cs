using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IgusSwarovskiHmi.Clients;
using IgusSwarovskiHmi.Enums;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Threading;
using OpenTK;
using System.Windows.Input;
using System.Windows.Controls;
using SharpDX.DirectInput;
using SharpVectors.Dom.Css;
using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using IgusSwarovskiHmi.Messages;
using System.Threading;
using System.IO;

namespace IgusSwarovskiHmi.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {

        readonly HardwareProtocolClient client;

        readonly DispatcherTimer dispatcherTimer;

        readonly DispatcherTimer controllerTimer;

        readonly DispatcherTimer airBurstTimer;

        readonly double[] jogValues;

        [ObservableProperty]
        private double jogSpeed;

        [ObservableProperty]
        private string ipAddress;


        private Joystick controller;
        private int joystickAxisRange;

        [ObservableProperty]
        private bool showProgramSelection = false;

        public SelectProgramViewModel SelectProgramViewModel { get; set; }

        public MainViewModel()
        {

            SelectProgramViewModel = new SelectProgramViewModel();

            client = new HardwareProtocolClient();
            IpAddress = "192.168.3.11";

            ConnectionStatus = "Disconnected";

            dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Interval = TimeSpan.FromMilliseconds(100);
            dispatcherTimer.IsEnabled = true;
            dispatcherTimer.Tick += dispatcherTimer_Tick;

            jogValues = new double[9];

            jogTimer = new DispatcherTimer();
            jogTimer.Interval = TimeSpan.FromMilliseconds(50);
            jogTimer.IsEnabled = false;
            jogTimer.Tick += jogTimer_Tick;
            JogSpeed = 10.0;

            controllerTimer = new DispatcherTimer();
            controllerTimer.Interval = TimeSpan.FromMilliseconds(50);
            controllerTimer.IsEnabled = false;
            controllerTimer.Tick += controllerTimer_Tick;

            airBurstTimer = new DispatcherTimer();
            airBurstTimer.Interval = TimeSpan.FromMilliseconds(500);
            airBurstTimer.IsEnabled = false;
            airBurstTimer.Tick += airBurstTimer_Tick;

            WeakReferenceMessenger.Default.Register<Messages.SelectProgramMessage>(this, (r, m) => UploadProgram(m.FilePath));

        }

        private void airBurstTimer_Tick(object? sender, EventArgs e)
        {
            AirOff();
            airBurstTimer.IsEnabled = false;
        }

        [RelayCommand]
        private void EnableGamepad()
        {
            try
            {
                var directInput = new DirectInput();
                Guid guid = Guid.Empty;
                controller = null;

                var allDevices = directInput.GetDevices();

                // Find a Joystick Guid
                foreach (var deviceInstance in directInput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AllDevices))
                    guid = deviceInstance.InstanceGuid;
                //create joystick
                if (guid != Guid.Empty)
                    controller = new SharpDX.DirectInput.Joystick(directInput, guid);

                Log.Debug("Found Joystick/Gamepad with GUID: {0}", guid);

                // Query all suported ForceFeedback effects
                var allEffects = controller.GetEffects();
                foreach (var effectInfo in allEffects)
                    Log.Debug("Effect available {0}", effectInfo.Name);

                // Set BufferSize in order to use buffered data.
                controller.Properties.BufferSize = 128;

                controller.Properties.AxisMode = DeviceAxisMode.Absolute;

                // Acquire the joystick
                controller.Acquire();

                var axisInfo = controller.GetObjectPropertiesByName("X");
                joystickAxisRange = axisInfo.Range.Maximum;
            } catch(Exception e)
            {
                Log.Error("Error while enabling gamepad!");
                return;
            }

            jogTimer.IsEnabled = false;
            controllerTimer.IsEnabled = true;

            UseTouchPanel = false;

        }

        [RelayCommand]
        private void DisableGamepad()
        {
            
            controllerTimer.IsEnabled = false;
            ResetJogValues();
            jogTimer.IsEnabled = true;
            UseTouchPanel = true;

        }

        [ObservableProperty]
        private bool toggleVacuumGamepad = false;

        [ObservableProperty]
        private bool useTouchPanel = true;

        [ObservableProperty]
        private string currentProgram;

        [RelayCommand]
        private void ChangeProgram()
        {
            SelectProgramViewModel.ShowSelectProgram = true;
        }

        private void UploadProgram(string filePath)
        {
            string progName = Path.GetFileName(filePath);
            StreamReader sr;
            string line;
            string msg;

            // anzahl der Zeilen im Programm herausbekommen
            int nrOfLines = 0;
            sr = new StreamReader(filePath);
            while (!sr.EndOfStream)
            {
                line = sr.ReadLine();
                nrOfLines++;
            }

            // dann übertragen
            msg = "CMD UploadFileInit ";
            msg += "Programs/" + progName;
            msg += " ";
            msg += nrOfLines;
            client.SendCommand(msg);

            sr = new StreamReader(filePath);
            for (int i = 0; i < nrOfLines; i++)
            {
                System.Threading.Thread.Sleep(10);
                line = sr.ReadLine();

                msg = "CMD UploadFileLine ";
                msg += line;
                client.SendCommand(msg);

            }
            System.Threading.Thread.Sleep(10);
            msg = "CMD UploadFileFinish";
            client.SendCommand(msg);

            string cmdText = "CMD LoadProgram ";
            cmdText += progName;
            client.SendCommand(cmdText);

        }

        private void controllerTimer_Tick(object? sender, EventArgs e)
        {
            var data = controller.GetCurrentState();

            double speedX;
            double speedY;
            if (GamepadSwitchAxis)
            {
                 speedY = ((double)data.X / (double)joystickAxisRange) - 0.5;
                 speedX = ((double)data.Y / (double)joystickAxisRange) - 0.5;
            } else
            {
                 speedX = ((double)data.X / (double)joystickAxisRange) - 0.5;
                 speedY = ((double)data.Y / (double)joystickAxisRange) - 0.5;
            }

            if (GamepadInvertX)
            {
                speedX *= -1;
            }

            if (GamepadInvertY)
            {
                speedY *= -1;
            }

            double speedZ = ((double)data.Z / (double)joystickAxisRange) - 0.5;

            double deadZone = 0.05;

            if (Math.Abs(speedX) > 0.1)
            {
                jogValues[0] = speedX * 2 * JogSpeed;
            }
            else
            {
                jogValues[0] = 0.0;
            }

            if (Math.Abs(speedY) > 0.1)
            {
                jogValues[1] = speedY * 2 * JogSpeed;
            }
            else
            {
                jogValues[1] = 0.0;
            }

            if (Math.Abs(speedZ) > 0.1)
            {
                jogValues[2] = speedZ * 2 * JogSpeed;
            }
            else
            {
                jogValues[2] = 0.0;
            }


            //Log.Debug("X = {0}, Y = {1}, Z = {2}", speedX, speedY, speedZ);

            if (data.Buttons[1])
            {
                if (VacuumState == "Off")
                {
                    VacuumOn();
                }
            }
            else
            {
                if (VacuumState == "On" && !ToggleVacuumGamepad)
                {
                    VacuumOff();
                }
            }

            if (data.Buttons[2])
            {
                if (VacuumState == "On")
                {
                    VacuumOff();
                }
                if (AirState == "Off")
                {
                    AirOn();
                }
            }
            else
            {
                if (AirState == "On")
                {
                    AirOff();
                }
            }

            if (data.Buttons[6])
            {
                jogValues[3] = JogSpeed;
            } 

            if (data.Buttons[7])
            {
                jogValues[3] = -JogSpeed;
            }

            if (!(data.Buttons[6] || data.Buttons[7]))
            {
                jogValues[3] = 0.0;
            }


            if (data.Buttons[4])
            {
                jogValues[4] = JogSpeed;
            }

            if (data.Buttons[5])
            {
                jogValues[4] = -JogSpeed;
            }

            if (!(data.Buttons[4] || data.Buttons[5]))
            {
                jogValues[4] = 0.0;
            }


            if (data.Buttons[3])
            {
                jogValues[5] = JogSpeed;
            }

            if (data.Buttons[0])
            {
                jogValues[5] = -JogSpeed;
            }

            if (!(data.Buttons[3] || data.Buttons[0]))
            {
                jogValues[5] = 0.0;
            }


            if (data.Buttons[9])
            {
                IncreaseSpeed();
            }

            if (data.Buttons[8])
            {
                DecreaseSpeed();
            }


            client.SetJogValues(jogValues);
        }

        [ObservableProperty]
        private string vacuumState = "Off";

        [ObservableProperty]
        private string airState = "Off";

        [ObservableProperty]
        private MovementMode movementMode = MovementMode.Joint;

        private void SetMovementMode(MovementMode mode)
        {
            switch (mode)
            {
                case MovementMode.Joint:
                    client.SendCommand("CMD MotionTypeJoint");
                    JogXButtonText = "A1";
                    JogYButtonText = "A2";
                    JogZButtonText = "A3";
                    JogAButtonText = "A4";
                    JogBButtonText = "A5";
                    JogCButtonText = "A6";
                    break;
                case MovementMode.Base:
                    client.SendCommand("CMD MotionTypeCartBase");
                    JogXButtonText = "X";
                    JogYButtonText = "Y";
                    JogZButtonText = "Z";
                    JogAButtonText = "A";
                    JogBButtonText = "B";
                    JogCButtonText = "C";
                    break;
                case MovementMode.Tool:
                    client.SendCommand("CMD MotionTypeCartTool");
                    JogXButtonText = "X";
                    JogYButtonText = "Y";
                    JogZButtonText = "Z";
                    JogAButtonText = "A";
                    JogBButtonText = "B";
                    JogCButtonText = "C";
                    break;
                default:
                    break;
            }
        }

        partial void OnMovementModeChanged(MovementMode oldValue, MovementMode newValue)
        {
            SetMovementMode(newValue);
        }

        [RelayCommand]
        private void Reset()
        {
            client.SendCommand("CMD Reset");
        }

        [RelayCommand]
        private void Enable()
        {
            client.SendCommand("CMD Enable");
        }

        private void jogTimer_Tick(object? sender, EventArgs e)
        {


            client.SetJogValues(jogValues);
        }

        private string[] jointLabels = new string[] { "A1", "A2", "A3", "A4", "A5", "A6", "E1", "E2", "E3" };
        private string[] coordinateLabels = new string[] { "X", "Y", "Z", "A", "B", "C" };
        private string[] coordinateUnits = new string[] { "mm", "mm", "mm", "°", "°", "°" };


        private void dispatcherTimer_Tick(object? sender, EventArgs e)
        {
            if (client.flagConnected)
            {
                ConnectionStatus = "Connected";
            }
            else
            {
                ConnectionStatus = "Disconnected";
            }

            JointsPositionString = string.Empty;
            JointsCurrentString = string.Empty;
            for (int i = 0; i < 6; i++)
            {

                JointsPositionString += jointLabels[i] + " = " + client.posJointsCurrent[i].ToString("0.0") + "°";
                JointsPositionString += System.Environment.NewLine;
                JointsCurrentString += jointLabels[i] + " = " + client.currentJoints[i].ToString("0.0") + " mA";
                JointsCurrentString += System.Environment.NewLine;

            }


            CartPositionString = string.Empty;
            for (int i = 0; i < 6; i++)
            {
                CartPositionString += coordinateLabels[i] + " = " + client.posCartesian[i].ToString("0.0") + " " + coordinateUnits[i];
                CartPositionString += System.Environment.NewLine;

            }

            EStopString = client.emergencyStopStatus.ToString();
            CntString = client.statusCnt.ToString();

            ErrorStatusString = client.errorString;
            Error123String = client.errorCodes[0] + " " + client.errorCodes[1] + " " + client.errorCodes[2];
            Error456String = client.errorCodes[3] + " " + client.errorCodes[4] + " " + client.errorCodes[5];
            Error789String = client.errorCodes[6] + " " + client.errorCodes[7] + " " + client.errorCodes[8];

            string binaryString = Convert.ToString((long)client.digialOutputs, 2).PadLeft(64, '0');

            int[] digitalOutputBits = binaryString.Select(c => int.Parse(c.ToString())) // convert each char to int
             .ToArray(); // Convert IEnumerable from select to Array

            if (digitalOutputBits[33] == 1)
            {
                VacuumState = "On";
            }
            else
            {
                VacuumState = "Off";
            }

            if (digitalOutputBits[32] == 1)
            {
                AirState = "On";
            }
            else
            {
                AirState = "Off";
            }

            CurrentProgram = client.programName;

            


        }

        [ObservableProperty]
        private bool loopProgram;

        [ObservableProperty]
        private bool hideAliveMessages = true;

        partial void OnHideAliveMessagesChanged(bool oldValue, bool newValue)
        {
            client.flagHideAliveMessages = newValue;
        }

        [ObservableProperty]
        private bool hideBasicStatusMessages = true;

        partial void OnHideBasicStatusMessagesChanged(bool oldValue, bool newValue)
        {
            client.flagHideBasicStatusMessages = newValue;
        }

        [ObservableProperty]
        private bool hidePlatformStatusMessages = true;
        partial void OnHidePlatformStatusMessagesChanged(bool oldValue, bool newValue)
        {
            client.flagHidePlatformStatusMessages = newValue;
        }

        [ObservableProperty]
        private bool hideFurtherStatusMessages = true;
        partial void OnHideFurtherStatusMessagesChanged(bool oldValue, bool newValue)
        {
            client.flagHideFurtherStatusMessages = newValue;
        }

        [ObservableProperty]
        private bool hideUnknownMessages = false;
        partial void OnHideUnknownMessagesChanged(bool oldValue, bool newValue)
        {
            client.flagHideUnknownMessages = newValue;
        }

        partial void OnJogSpeedChanged(double oldValue, double newValue)
        {
            client.SetOverride(newValue);
        }

        [ObservableProperty]
        private string connectionStatus;

        [ObservableProperty]
        private string cntString;

        [ObservableProperty]
        private string eStopString;

        [ObservableProperty]
        private string errorStatusString;

        [ObservableProperty]
        private string error123String;


        [ObservableProperty]
        private string error456String;


        [ObservableProperty]
        private string error789String;

        [ObservableProperty]
        private string jogXButtonText = "A1";
        [ObservableProperty]
        private string jogYButtonText = "A2";
        [ObservableProperty]
        private string jogZButtonText = "A3";
        [ObservableProperty]
        private string jogAButtonText = "A4";
        [ObservableProperty]
        private string jogBButtonText = "A5";
        [ObservableProperty]
        private string jogCButtonText = "A6";



        [RelayCommand]
        private void StartProgram()
        {
            if (LoopProgram)
            {
                client.SendCommand("CMD ProgramReplayMode 1");
            } else
            {
                client.SendCommand("CMD ProgramReplayMode 0");
            }
            client.SendCommand("CMD StartProgram");
        }

        [RelayCommand]
        private void StopProgram()
        {
            client.SendCommand("CMD StopProgram");
        }

        private DispatcherTimer jogTimer;

        [RelayCommand]
        private void StartJogXPlus()
        {

            jogTimer.IsEnabled = true;
            jogValues[0] = JogSpeed;

        }

        [RelayCommand]
        private void StopJog()
        {
            ResetJogValues();
            jogTimer.IsEnabled = false;
            client.SetJogValues(jogValues);
        }



        [RelayCommand]
        private void StartJogXMinus()
        {

            jogTimer.IsEnabled = true;
            jogValues[0] = -JogSpeed;

        }


        [RelayCommand]
        private void StartJogYPlus()
        {

            jogTimer.IsEnabled = true;
            jogValues[1] = JogSpeed;

        }



        [RelayCommand]
        private void StartJogYMinus()
        {

            jogTimer.IsEnabled = true;
            jogValues[1] = -JogSpeed;

        }



        [RelayCommand]
        private void StartJogZPlus()
        {

            jogTimer.IsEnabled = true;
            jogValues[2] = JogSpeed;

        }



        [RelayCommand]
        private void StartJogZMinus()
        {

            jogTimer.IsEnabled = true;
            jogValues[2] = -JogSpeed;

        }


        [RelayCommand]
        private void StartJogAPlus()
        {

            jogTimer.IsEnabled = true;
            jogValues[3] = JogSpeed;

        }


        [RelayCommand]
        private void StartJogAMinus()
        {

            jogTimer.IsEnabled = true;
            jogValues[3] = -JogSpeed;

        }

        [RelayCommand]
        private void StartJogBPlus()
        {

            jogTimer.IsEnabled = true;
            jogValues[4] = JogSpeed;

        }


        [RelayCommand]
        private void StartJogBMinus()
        {

            jogTimer.IsEnabled = true;
            jogValues[4] = -JogSpeed;

        }

        [RelayCommand]
        private void StartJogCPlus()
        {

            jogTimer.IsEnabled = true;
            jogValues[5] = JogSpeed;

        }


        [RelayCommand]
        private void StartJogCMinus()
        {

            jogTimer.IsEnabled = true;
            jogValues[5] = -JogSpeed;

        }


        [RelayCommand]
        private void IncreaseSpeed()
        {
            if (JogSpeed == 0.1)
            {
                JogSpeed = 0.5;
                return;
            }

            if (JogSpeed == 0.5)
            {
                JogSpeed = 1.0;
                return;
            }

            if (JogSpeed == 1.0)
            {
                JogSpeed = 2.0;
                return;
            }

            if (JogSpeed == 2.0)
            {
                JogSpeed = 5.0;
                return;
            }

            if (JogSpeed == 5.0)
            {
                JogSpeed = 10.0;
                return;
            }

            if (JogSpeed < 100.0)
            {
                JogSpeed += 10.0;
                return;
            }

        }

        [ObservableProperty]
        private string jointsPositionString;

        [ObservableProperty]
        private string jointsCurrentString;

        [ObservableProperty]
        private string cartPositionString;

        [RelayCommand]
        private void MoveHome()
        {
            string userCommand = string.Format("CMD Move Joint {0} {1} {2} {3} {4} {5} 0 0 0 {6}", 0.0, 0.0, 90.0, 0.0, 90.0, 0.0, JogSpeed);
            client.SendCommand(userCommand);
        }

        [RelayCommand]
        private void ZeroJoints()
        {
            string userCommand = string.Format("CMD Move Joint {0} {1} {2} {3} {4} {5} 0 0 0 {6}", 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, JogSpeed);
            //string userCommand = "CMD SetJointsToZero";
            client.SendCommand(userCommand);
        }

        [RelayCommand]
        private void VacuumOn()
        {
            string userCommand = "CMD DOUT 30 true";
            client.SendCommand(userCommand);
        }

        [RelayCommand]
        private void VacuumOff()
        {
            string userCommand = "CMD DOUT 30 false";
            client.SendCommand(userCommand);
        }

        [RelayCommand]
        private void VacuumOffWithAirburst()
        {
            string userCommand = "CMD DOUT 30 false";
            client.SendCommand(userCommand);

            
            userCommand = "CMD DOUT 31 true";
            client.SendCommand(userCommand);

            airBurstTimer.IsEnabled = true;

        }

        [ObservableProperty]
        private bool gamepadSwitchAxis = true;

        [ObservableProperty]
        private bool gamepadInvertX = false;

        [ObservableProperty]
        private bool gamepadInvertY = false;

        
        [RelayCommand]
        private void RestartApp()
        {
            var currentExecutablePath = Process.GetCurrentProcess().MainModule.FileName;
            Process.Start(currentExecutablePath);
            

            Thread.Sleep(1000);
            

            Application.Current.Shutdown();
            Environment.Exit(0);
        }

        [RelayCommand]
        private void AirOn()
        {


            string userCommand = "CMD DOUT 31 true";
            client.SendCommand(userCommand);
        }

        [RelayCommand]
        private void AirOff()
        {
            string userCommand = "CMD DOUT 31 false";
            client.SendCommand(userCommand);
        }

        [RelayCommand]
        private void DecreaseSpeed()
        {
            if (JogSpeed == 0.5)
            {
                JogSpeed = 0.1;
                return;
            }

            if (JogSpeed == 1.0)
            {
                JogSpeed = 0.5;
                return;
            }

            if (JogSpeed == 2.0)
            {
                JogSpeed = 1.0;
                return;
            }

            if (JogSpeed == 5.0)
            {
                JogSpeed = 2.0;
                return;
            }

            if (JogSpeed == 10.0)
            {
                JogSpeed = 5.0;
                return;
            }

            if (JogSpeed > 10.0)
            {
                JogSpeed -= 10.0;
                return;
            }
        }

        private void ResetJogValues()
        {

            for (int i = 0; i < jogValues.Length; i++)
            {
                jogValues[i] = 0.0;
            }
        }

        [RelayCommand]
        private void ConnectClient()
        {
            client.SetIPAddress(IpAddress);
            if (!client.GetConnectionStatus())
            {
                client.Connect();
                ResetJogValues();
                client.SetJogValues(jogValues);

                SetMovementMode(MovementMode);

                Log.Information("Interface: Connecting...");

            }
            else
            {
                Log.Information("Interface: Already connected");
            }
        }

        [RelayCommand]
        private void DisconnectClient()
        {
            client.Disconnect();
            Log.Information("Client: Disconnected");
        }

    }
}
