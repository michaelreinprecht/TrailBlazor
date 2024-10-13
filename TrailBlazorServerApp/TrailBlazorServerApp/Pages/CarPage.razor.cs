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

        // Timer to call SendCommandToESP every 500ms
        private System.Timers.Timer? udpSendTimer;
        private string statusMessage = "";
        private List<string> receivedMessages = new();

        // Reference to the container div to programmatically focus
        private ElementReference arrowContainer;

        // A set to keep track of currently pressed keys
        private HashSet<string> pressedKeys = new();

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
            int speed = 10; //Default to 10 for now
            char direction = GetDirection();
            bool stop = false; //Default to false for now

            ControlCommand controlCommand = new ControlCommand();
            controlCommand.Speed = speed; 
            controlCommand.Direction = (byte)direction;
            controlCommand.Stop = (byte)(stop ? 1 : 0); 

            if (UdpService != null)
            {
                await UdpService.SendDataToEspDevices(MessageType.ControlCommand, controlCommand, Flags.ACK_FLAG);
                statusMessage = $"Sent Command: Direction = {direction}, Speed = {controlCommand.Speed}, Stop = {(stop ? "True" : "False")}";
                StateHasChanged(); // Refresh the UI to reflect the latest status message
            }
        }

        private void HandleMessageReceived(string message)
        {
            Console.WriteLine($"Received message: {message}"); // Log the message for debugging
            receivedMessages.Add(message);
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

            // Determine the direction based on the active keys
            if (up && right)
                return 'B'; // Up-Right
            if (down && right)
                return 'D'; // Down-Right
            if (down && left)
                return 'F'; // Down-Left
            if (up && left)
                return 'H'; // Up-Left

            if (up)
                return 'A'; // Forward
            if (right)
                return 'C'; // Right
            if (down)
                return 'E'; // Down
            if (left)
                return 'G'; // Left

            return 'X'; // No direction or opposing directions cancel each other out
        }

        // Initialize the timer and start calling SendCommandToESP every 500ms
        private void StartTimer()
        {
            udpSendTimer = new Timer(500); // 500ms interval
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
                udpSendTimer.Dispose(); // Dispose of the timer
                udpSendTimer = null; // Set to null to avoid further access
            }

            // Unsubscribe from UdpService events to prevent memory leaks
            if (UdpService != null)
            {
                UdpService.OnMessageReceived -= HandleMessageReceived;
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
            if (UdpService != null)
            {
                UdpService.OnMessageReceived += HandleMessageReceived; // Subscribe to the event
                var cancellationToken = new CancellationTokenSource().Token;
                await UdpService.StartListeningForResponses(cancellationToken);
            }
            else
            {
                Console.WriteLine("UdpService is null! Make sure it's injected correctly.");
            }
        }


        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                // Set focus on the container div so it can capture key presses
                await arrowContainer.FocusAsync();

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
