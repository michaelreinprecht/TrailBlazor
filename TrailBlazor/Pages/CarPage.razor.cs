using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace TrailBlazor.Pages
{
    public partial class CarPage : ComponentBase
    {
        [Parameter]
        public int carID { get; set; }

        private string upArrowColor = "black";
        private string leftArrowColor = "black";
        private string downArrowColor = "black";
        private string rightArrowColor = "black";

        // Reference to the container div to programmatically focus
        private ElementReference arrowContainer;

        // A set to keep track of currently pressed keys
        private HashSet<string> pressedKeys = new();

        [Inject]
        private IJSRuntime JSRuntime { get; set; }

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

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
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

                // Set focus on the container div so it can capture key presses
                await arrowContainer.FocusAsync();
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
