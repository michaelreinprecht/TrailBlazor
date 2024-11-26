using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Timers;
using TrailBlazorServerApp.Data;
using Timer = System.Timers.Timer;

namespace TrailBlazorServerApp.Pages
{
    public partial class CarPage : ComponentBase, IDisposable
    {

        [Parameter]
        public int carID { get; set; }

        private string upArrowColor = "black";
        private string leftArrowColor = "black";
        private string downArrowColor = "black";
        private string rightArrowColor = "black";

        private Timer? udpSendTimer;
        private string statusMessage = "";
        private List<string> receivedMessages = new();

        // Reference to the container div to programmatically focus
        private ElementReference arrowContainer;

        // A set to keep track of currently pressed keys
        private HashSet<string> pressedKeys = new();

        private bool isMobile = false;
        private bool isGasHeld = false;

        private bool isConnecting = true;
        private Timer? ackTimer;

        [Inject]
        private IJSRuntime? JSRuntime { get; set; }

        // This method handles the keydown event (when a key is pressed)
        private void HandleKeyDown(KeyboardEventArgs e)
        {
            // Add the key to the set of pressed keys
            pressedKeys.Add(e.Key.ToLower());

            // Update the colors of the arrows based on currently pressed keys
            UpdateArrowColors();
        }

        // This method handles the keyup event (when a key is released)
        private void HandleKeyUp(KeyboardEventArgs e)
        {
            // Remove the key from the set of pressed keys
            pressedKeys.Remove(e.Key.ToLower());

            // Update the colors of the arrows based on currently pressed keys
            UpdateArrowColors();
        }

        private void OnGasPressed()
        {
            isGasHeld = true;
        }

        private void OnGasReleased()
        {
            isGasHeld = false;
        }

        // This method updates the colors of the arrows based on which keys are pressed
        private void UpdateArrowColors()
        {
            // Set the arrow colors based on the pressed keys
            upArrowColor = pressedKeys.Contains("w") ? "green" : "black";
            leftArrowColor = pressedKeys.Contains("a") ? "green" : "black";
            downArrowColor = pressedKeys.Contains("s") ? "green" : "black";
            rightArrowColor = pressedKeys.Contains("d") ? "green" : "black";

            // Refresh the UI to apply the changes
            StateHasChanged();
        }

        private async void SendCommandToESP()
        {
            string carIPAddress = "";
            if (carID == 1)
            {
                carIPAddress = "192.168.4.100";
            } else if (carID == 2)
            {
                carIPAddress = "192.168.4.101";
            }

            int speed = 10; //Default to 10 for now
            char direction = GetDirection();
            bool stop = false; //Default to false for now

            ControlCommand controlCommand = new ControlCommand();
            controlCommand.Speed = speed;
            controlCommand.Direction = (byte)direction;
            controlCommand.Stop = (byte)(stop ? 1 : 0);

            if (ProtocolService != null)
            {
                await ProtocolService.SendProtocolMessage(carIPAddress, MessageType.ControlCommand, controlCommand, new HashSet<Flag>() { Flag.ACK_Flag });
                statusMessage = $"Sent Command: Direction = {direction}, Speed = {controlCommand.Speed}, Stop = {(stop ? "True" : "False")}";
                StateHasChanged(); // Refresh the UI to reflect the latest status message
            }
        }

        private void HandleMessageReceived(string message)
        {
            Console.WriteLine($"Received message: {message}"); // Log the message for debugging
            receivedMessages.Add(message); // Add to the message list

            isConnecting = false; // Hide the loading screen since we received a message
            ResetAckTimer(); // Restart the timer for the next 5-second window

            InvokeAsync(StateHasChanged); // Update the UI
        }


        private char GetDirection()
        {
            bool up = pressedKeys.Contains("w");
            bool right = pressedKeys.Contains("d");
            bool down = pressedKeys.Contains("s");
            bool left = pressedKeys.Contains("a");

            // Opposing directions cancel each other out
            if (up && down)
            {
                up = false;
                down = false;
            }
            if (left && right)
            {
                left = false;
                right = false;
            }

            //On Mobile the gas button has to be held for the car to move.
            if (isMobile && !isGasHeld)
            {
                return '0';
            }

            // Determine the direction based on the active keys
            if (up && right)
                return '2'; // Up-Right
            if (down && right)
                return '4'; // Down-Right
            if (down && left)
                return '6'; // Down-Left
            if (up && left)
                return '8'; // Up-Left

            if (up)
                return '1'; // Forward
            if (right)
                return '3'; // Right
            if (down)
                return '5'; // Down
            if (left)
                return '7'; // Left

            return '0'; // No direction or opposing directions cancel each other out
        }

        // Initialize the timer and start calling SendCommandToESP every 500ms
        private void StartTimer()
        {
            udpSendTimer = new Timer(200); // 200ms interval
            udpSendTimer.Elapsed += OnTimerElapsed; // Hook up the event handler
            udpSendTimer.AutoReset = true; // Restart the timer after each interval
            udpSendTimer.Enabled = true; // Enable the timer
        }

        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            InvokeAsync(() => SendCommandToESP()); // Call SendCommandToESP on the main thread
        }

        void IDisposable.Dispose()
        {
            // Stop the timer if it exists
            if (udpSendTimer != null)
            {
                udpSendTimer.Stop(); // Stop the timer
                udpSendTimer.Elapsed -= OnTimerElapsed; // Unsubscribe from the event
                udpSendTimer.Enabled = false;
                udpSendTimer.Dispose(); // Dispose of the timer
                udpSendTimer = null; // Set to null to avoid further access
            }

            // Unsubscribe from UdpService events to prevent memory leaks
            if (ProtocolService != null)
            {
                ProtocolService.OnMessageReceived -= HandleMessageReceived;
            }
        }

        private void HandleDirectionDown(string direction)
        {
            if (!pressedKeys.Contains(direction))
            {
                pressedKeys.Add(direction);
            }
            UpdateArrowColors();
        }

        private void HandleDirectionUp(string direction)
        {
            if (pressedKeys.Contains(direction))
            {
                pressedKeys.Remove(direction);
            }
            UpdateArrowColors();
        }

        protected override async Task OnInitializedAsync()
        {
            StartAckTimer();
            if (ProtocolService != null)
            {
                ProtocolService.OnMessageReceived += HandleMessageReceived; // Subscribe to the event
                var cancellationToken = new CancellationTokenSource().Token;
                await ProtocolService.StartListening(cancellationToken);
            }
            else
            {
                Console.WriteLine("ProtocolService is null! Make sure it's injected correctly.");
            }

        }

        private void StartAckTimer()
        {
            if (ackTimer != null)
            {
                ackTimer.Dispose();
            }
            ackTimer = new Timer(5000); // 5 seconds
            ackTimer.Elapsed += AckTimeout;
            ackTimer.AutoReset = false; // Run only once unless restarted
            ackTimer.Start();
        }

        private void ResetAckTimer()
        {
            //ackTimer.Elapsed -= AckTimeout;
            //ackTimer.Dispose();
            //StartAckTimer();
            ackTimer.Stop(); // Stop the timer
            ackTimer.Start(); // Restart the timer
        }

        private void AckTimeout(object? sender, ElapsedEventArgs e)
        {
            isConnecting = true; // No messages received, show the loading screen
            Console.WriteLine("No messages received for 5 seconds. Showing loading screen.");
            InvokeAsync(StateHasChanged); // Update the UI
        }




        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (ackTimer == null)
            {
                StartAckTimer();
            }

            if (firstRender)
            {
                await AdaptToDeviceType();

                if (!isConnecting)
                {
                    await arrowContainer.FocusAsync();
                }
               

                bool success = false;
                int retryCount = 0;
                const int maxRetries = 5;
                const int delayMs = 1000; // 1 second delay between retries

                while (!success && retryCount < maxRetries)
                {
                    try
                    {
                        await JSRuntime.InvokeVoidAsync("deviceTilt.startListening", DotNetObjectReference.Create(this));
                        success = true; // Exit loop if successful
                    }
                    catch (JSException jsEx)
                    {
                        Console.WriteLine($"JavaScript Exception: {jsEx.Message}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception: {ex.Message}");
                    }

                    if (!success)
                    {
                        retryCount++;
                        await Task.Delay(delayMs); // Wait before retrying
                    }
                }

                // Start the timer after everything is initialized
                StartTimer();
            }
        }

        private async Task AdaptToDeviceType()
        {
            var deviceType = await JSRuntime.InvokeAsync<string>("deviceHelper.getDeviceType");

            if (deviceType == "mobile")
            {
                isMobile = true;
            }
        }

        // Method called from JavaScript when the device tilts
        [JSInvokable]
        public void OnTiltChange(double tiltX, double tiltY)
        {
            // Clear previous keys
            pressedKeys.Clear();

            // Simple tilt logic: Adjust these thresholds based on sensitivity
            if (tiltX < 35) // Tilt forward (up)
            {
                pressedKeys.Add("w");
            }
            else if (tiltX > 45) // Tilt backward (down)
            {
                pressedKeys.Add("s");
            }

            if (tiltY < -5) // Tilt left
            {
                pressedKeys.Add("a");
            }
            else if (tiltY > 5) // Tilt right
            {
                pressedKeys.Add("d");
            }

            // Update the arrow colors based on the tilt
            UpdateArrowColors();
        }
    }
}
