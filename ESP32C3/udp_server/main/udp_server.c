#include <string.h>
#include <sys/param.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "esp_system.h"
#include "esp_wifi.h"
#include "esp_event.h"
#include "esp_log.h"
#include "nvs_flash.h"
#include "esp_netif.h"
#include "protocol_examples_common.h"

#include "lwip/err.h"
#include "lwip/sockets.h"
#include "lwip/sys.h"
#include <lwip/netdb.h>

#define PORT 4444

static const char *TAG = "udp_server";
static EventGroupHandle_t wifi_event_group;

// typedef struct {
//     char direction;  // Direction character ('L', 'R', 'F', 'B', etc.)
//     int stop;        // Stop flag (0 or 1)
// } command_t;

static void wifi_event_handler(void *arg, esp_event_base_t event_base, int32_t event_id, void *event_data)
{
    int wifi_connected_bit = BIT0;
    if (event_base == WIFI_EVENT && event_id == WIFI_EVENT_STA_START)
    {
        // If Wi-Fi station mode has started, try to connect.
        esp_wifi_connect();
    }
    else if (event_base == WIFI_EVENT && event_id == WIFI_EVENT_STA_DISCONNECTED)
    {
        // If Wi-Fi station mode has disconnected, try to reconnect.
        ESP_LOGI(TAG, "Disconnected from Wi-Fi, retrying...");
        esp_wifi_connect();
        xEventGroupClearBits(wifi_event_group, wifi_connected_bit);
    }
    else if (event_base == IP_EVENT && event_id == IP_EVENT_STA_GOT_IP)
    {
        // ESP has received IP-Address from the router, set connected bit.
        ip_event_got_ip_t *event = (ip_event_got_ip_t *)event_data;
        ESP_LOGI(TAG, "Got IP: %s",
                 ip4addr_ntoa(&event->ip_info.ip));
        xEventGroupSetBits(wifi_event_group, wifi_connected_bit);
    }
}

void wifi_init_static_ip(void)
{
    // Create Wi-Fi interface in station mode.
    esp_netif_t *netif = esp_netif_create_default_wifi_sta();
    assert(netif);

    esp_netif_ip_info_t ip_info;

    // Set IP configuration for ESP
    IP4_ADDR(&ip_info.ip, 192, 168, 4, 9);        // ESP Address
    IP4_ADDR(&ip_info.gw, 192, 168, 4, 1);        // Gateway (Raspi) address
    IP4_ADDR(&ip_info.netmask, 255, 255, 255, 0); // Netmask

    esp_netif_dhcpc_stop(netif);            // Stop DHCP client
    esp_netif_set_ip_info(netif, &ip_info); // Assign static IP info

    // Initialize the Wi-Fi with default config
    wifi_init_config_t cfg = WIFI_INIT_CONFIG_DEFAULT();
    ESP_ERROR_CHECK(esp_wifi_init(&cfg));

    // Configure Wi-Fi connection (using WPA2 for authentication)
    wifi_config_t wifi_config = {
        .sta = {
            .ssid = "TrailBlazorRaspi",
            .password = "admin1234!",
            .threshold.authmode = WIFI_AUTH_WPA2_PSK,
        },
    };

    // Sets Wi-Fi mode to station and applies Wi-Fi configuration.
    ESP_ERROR_CHECK(esp_wifi_set_mode(WIFI_MODE_STA));
    ESP_ERROR_CHECK(esp_wifi_set_config(WIFI_IF_STA, &wifi_config));

    // Register the event handler to handle Wifi and Ip events
    ESP_ERROR_CHECK(esp_event_handler_instance_register(WIFI_EVENT, ESP_EVENT_ANY_ID, &wifi_event_handler, NULL, NULL));
    ESP_ERROR_CHECK(esp_event_handler_instance_register(IP_EVENT, IP_EVENT_STA_GOT_IP, &wifi_event_handler, NULL, NULL));

    ESP_ERROR_CHECK(esp_wifi_start());

    ESP_LOGI(TAG, "wifi_init_static_ip finished.");
}

static void udp_server_task(void *pvParameters)
{
    char rx_buffer[128];          // Buffer for incoming packets
    char addr_str[128];           // Buffer for client IP address
    int addr_family = AF_INET;    // IPv4
    int ip_protocol = IPPROTO_IP; // using IP protocol
    struct sockaddr_in dest_addr; // struct to hold destination address for incoming packets

    while (1)
    {
        // Prepare to receive packets from any IP address via port 4444 (should change to Raspi Address eventually)
        dest_addr.sin_addr.s_addr = htonl(INADDR_ANY);
        dest_addr.sin_family = AF_INET;
        dest_addr.sin_port = htons(PORT);

        // Attempt to create socket and handle errors in socket creation
        int sock = socket(addr_family, SOCK_DGRAM, ip_protocol);
        if (sock < 0)
        {
            ESP_LOGE(TAG, "Unable to create socket: errno %d", errno);
            break;
        }
        ESP_LOGI(TAG, "Socket created");

        // Set a timeout of 10 seconds for receiving data (socket will restart if no data is received within timeout)
        struct timeval timeout;
        timeout.tv_sec = 10;
        timeout.tv_usec = 0;
        setsockopt(sock, SOL_SOCKET, SO_RCVTIMEO, &timeout, sizeof timeout);

        // Bind the socket to the destination address and specified port
        int err = bind(sock, (struct sockaddr *)&dest_addr, sizeof(dest_addr));
        if (err < 0)
        {
            ESP_LOGE(TAG, "Socket unable to bind: errno %d", errno);
        }
        ESP_LOGI(TAG, "Socket bound, port %d", PORT);

        // Enter loop waiting to receive UDP packets
        struct sockaddr_storage source_addr; // stored the senders address
        socklen_t socklen = sizeof(source_addr);

        while (1)
        {
            ESP_LOGI(TAG, "Waiting for data");
            // Attempt to receive packets and handle errors in receiving
            int len = recvfrom(sock, rx_buffer, sizeof(rx_buffer) - 1, 0, (struct sockaddr *)&source_addr, &socklen);
            if (len < 0)
            {
                // On failure to receive data, log error and break loop
                ESP_LOGE(TAG, "recvfrom failed: errno %d", errno);
                break;
            }
            else
            {
                // On successful reception of data, log the data and echo it back to the sender
                inet_ntoa_r(((struct sockaddr_in *)&source_addr)->sin_addr, addr_str, sizeof(addr_str) - 1);

                rx_buffer[len] = 0; // Null-terminate whatever we received and treat like a string...
                ESP_LOGI(TAG, "Received %d bytes from %s:", len, addr_str);
                ESP_LOGI(TAG, "%s", rx_buffer);

                int err = sendto(sock, rx_buffer, len, 0, (struct sockaddr *)&source_addr, sizeof(source_addr));
                if (err < 0)
                {
                    ESP_LOGE(TAG, "Error occurred during sending: errno %d", errno);
                    break;
                }
            }
        }

        if (sock != -1)
        {
            ESP_LOGE(TAG, "Shutting down socket and restarting...");
            shutdown(sock, 0);
            close(sock);
        }
    }
    vTaskDelete(NULL);
}

void app_main(void)
{
    ESP_ERROR_CHECK(nvs_flash_init());
    ESP_ERROR_CHECK(esp_netif_init());
    ESP_ERROR_CHECK(esp_event_loop_create_default());

    wifi_event_group = xEventGroupCreate();
    wifi_init_static_ip();

    xTaskCreate(udp_server_task, "udp_server", 4096, NULL, 5, NULL);
}
