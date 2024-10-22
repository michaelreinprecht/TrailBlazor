window.deviceHelper = {
    getDeviceType: function () {
        const userAgent = navigator.userAgent || navigator.vendor || window.opera;
        if (/android|iPad|iPhone|iPod/.test(userAgent.toLowerCase())) {
            return 'mobile';
        }
        return 'desktop';
    }
}

