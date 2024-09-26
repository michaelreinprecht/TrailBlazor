#include <string.h>
#include <sys/param.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "lwip/sockets.h"
#include "esp_wifi.h"
#include "esp_event.h"
#include "esp_log.h"
#include "nvs_flash.h"
#include "protocol_examples_common.h"
#include "esp_mac.h"
#include <portmacro.h>

#define SERVER_IP_ADDR "192.168.4.1" // IP address of the Blazor App (Raspberry Pi) server
#define SERVER_PORT 4211             // Port on which Blazor App is listening
#define CONFIG_LOG_MAXIMUM_LEVEL "ERROR"
#define CONFIG_FREERTOS_HZ 1000

#define LOCAL_PORT 6005 // Port on which ESP32 will listen for responses

static const char *TAG = "udp_client_server";

// Buffer to hold incoming and outgoing data
char rx_buffer[128];
char tx_buffer[128];

// Function to send data to Blazor App
void udp_client_task(void *pvParameters)
{
    struct sockaddr_in dest_addr;
    dest_addr.sin_addr.s_addr = inet_addr(SERVER_IP_ADDR); // Server IP address
    dest_addr.sin_family = AF_INET;
    dest_addr.sin_port = htons(SERVER_PORT); // Server port

    int sock = socket(AF_INET, SOCK_DGRAM, IPPROTO_IP);
    if (sock < 0)
    {
        ESP_LOGE(TAG, "Unable to create socket: errno %d", errno);
        vTaskDelete(NULL);
        return;
    }
    ESP_LOGI(TAG, "Socket created, sending to %s:%d", SERVER_IP_ADDR, SERVER_PORT);

    while (1)
    {
        // Prepare a message to send (for example, sensor data)
        sprintf(tx_buffer, "Hello from ESP32");

        // Send message to Blazor App
        int err = sendto(sock, tx_buffer, strlen(tx_buffer), 0, (struct sockaddr *)&dest_addr, sizeof(dest_addr));
        if (err < 0)
        {
            ESP_LOGE(TAG, "Error occurred during sending: errno %d", errno);
        }
        else
        {
            ESP_LOGI(TAG, "Message sent: %s", tx_buffer);
        }

        // Wait for 2 seconds before sending the next message
        vTaskDelay(2000 / portTICK_PERIOD_MS);
    }

    if (sock != -1)
    {
        ESP_LOGE(TAG, "Shutting down socket and restarting...");
        shutdown(sock, 0);
        close(sock);
        vTaskDelete(NULL);
    }
}

// Function to listen for responses from the Blazor App
void udp_server_task(void *pvParameters)
{
    char addr_str[128];
    int addr_family = AF_INET;
    int ip_protocol = IPPROTO_IP;

    struct sockaddr_in dest_addr;
    dest_addr.sin_addr.s_addr = htonl(INADDR_ANY);
    dest_addr.sin_family = AF_INET;
    dest_addr.sin_port = htons(LOCAL_PORT); // ESP32's listening port

    int sock = socket(addr_family, SOCK_DGRAM, ip_protocol);
    if (sock < 0)
    {
        ESP_LOGE(TAG, "Unable to create socket: errno %d", errno);
        vTaskDelete(NULL);
        return;
    }

    int err = bind(sock, (struct sockaddr *)&dest_addr, sizeof(dest_addr));
    if (err < 0)
    {
        ESP_LOGE(TAG, "Socket unable to bind: errno %d", errno);
        close(sock);
        vTaskDelete(NULL);
        return;
    }
    ESP_LOGI(TAG, "Socket bound, port %d", LOCAL_PORT);

    while (1)
    {
        ESP_LOGI(TAG, "Waiting for data...");
        struct sockaddr_in source_addr;
        socklen_t socklen = sizeof(source_addr);
        int len = recvfrom(sock, rx_buffer, sizeof(rx_buffer) - 1, 0, (struct sockaddr *)&source_addr, &socklen);

        // Error occurred during receiving
        if (len < 0)
        {
            ESP_LOGE(TAG, "recvfrom failed: errno %d", errno);
            break;
        }
        // Data received
        else
        {
            rx_buffer[len] = 0; // Null-terminate whatever we received
            inet_ntoa_r(source_addr.sin_addr, addr_str, sizeof(addr_str) - 1);
            ESP_LOGI(TAG, "Received %d bytes from %s: %s", len, addr_str, rx_buffer);
        }
    }

    if (sock != -1)
    {
        ESP_LOGE(TAG, "Shutting down socket and restarting...");
        shutdown(sock, 0);
        close(sock);
        vTaskDelete(NULL);
    }
}

void app_main(void)
{
    // Initialize NVS
    esp_err_t ret = nvs_flash_init();
    if (ret == ESP_ERR_NVS_NO_FREE_PAGES || ret == ESP_ERR_NVS_NEW_VERSION_FOUND)
    {
        ESP_ERROR_CHECK(nvs_flash_erase());
        ret = nvs_flash_init();
    }
    ESP_ERROR_CHECK(ret);

    ESP_ERROR_CHECK(esp_netif_init());
    ESP_ERROR_CHECK(esp_event_loop_create_default());
    ESP_ERROR_CHECK(example_connect()); // Connect to Wi-Fi

    // Create tasks for sending and receiving UDP packets
    xTaskCreate(udp_client_task, "udp_client_task", 4096, NULL, 5, NULL);
    xTaskCreate(udp_server_task, "udp_server_task", 4096, NULL, 5, NULL);
}
