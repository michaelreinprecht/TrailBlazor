window.deviceTilt = {
    startListening: function (dotNetObj) {
        if (window.DeviceOrientationEvent) {
            window.addEventListener('deviceorientation', function (event) {
                let tiltX = event.beta; // Front-to-back tilt in degrees
                let tiltY = event.gamma; // Left-to-right tilt in degrees
                let tempTiltX = tiltX;
                let tempTiltY = tiltY;

                const orientation = window.screen.orientation.type;

                // Adjust tilt values based on screen orientation
                if (orientation.startsWith('landscape-primary')) {
                    console.log("landscape-primary");
                    tiltX = -tempTiltY;
                    tiltY = tempTiltX;
                } else if (orientation.startsWith('landscape-secondary')) {
                    // Rotated landscape orientation
                    console.log("landscape-secondary");
                    tiltX = tempTiltY;
                    tiltY = -tempTiltX;
                }
                else { //Default portrait mode, no adjustment needed

                }
                dotNetObj.invokeMethodAsync('OnTiltChange', tiltX, tiltY);
            });
        } else {
            console.error('DeviceOrientationEvent is not supported.');
        }
    }
};
