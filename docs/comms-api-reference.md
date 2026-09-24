# Comms API Reference (Pahappa / EgoSMS)

Reference documentation for integrating with the Comms Platform SMS API — sending SMS, checking balance, receiving transaction status updates, and using the official SDK.

## Table of Contents

- [Getting Started](#getting-started)
- [Authentication](#authentication)
- [Sending SMS](#sending-sms)
- [Checking Balance](#checking-balance)
- [Transaction Status Webhook](#transaction-status-webhook)
- [Error Handling](#error-handling)
- [SDK Reference](#sdk-reference)
- [Model Reference](#model-reference)

---

## Getting Started

1. Register for a free account on the Comms Platform.
2. For **testing/sandbox**, sign up via the sandbox link and ask customer care to top up your sandbox balance.
3. Get your API credentials: on the Comms Platform, go to **Settings → API Settings** and copy your **API Username** and **API Password**.

### API Endpoint

All requests are JSON POST requests to:

```
https://comms.egosms.co/api/v1/json/
```

- **Content-Type:** `application/json`
- **Authentication:** required on every request (see below)

---

## Authentication

Every request must include your credentials in a `userdata` object:

| Field | Type | Required | Description |
|---|---|---|---|
| `username` | string | Yes | Your Comms API username |
| `password` | string | Yes | Your Comms API key |

```json
"userdata": {
  "username": "your_username",
  "password": "your_api_key"
}
```

---

## Sending SMS

### Request Structure

| Field | Type | Required | Description |
|---|---|---|---|
| `method` | string | Yes | Must be `"SendSms"` |
| `userdata` | object | Yes | Authentication (see above) |
| `msgdata` | array | Yes | List of messages to send |
| `msgdata[].number` | string | Yes | Recipient's phone number, international format (e.g. `"256700111222"`) |
| `msgdata[].message` | string | Yes | SMS message content |
| `msgdata[].senderid` | string | Yes | Sender ID, as configured in your Comms account (≤ 11 characters) |
| `msgdata[].priority` | string | No | Message priority (`"0"` = normal; see [MessagePriority](#messagepriority-enum) for the SDK equivalent) |

### Response Structure

```json
{
  "Status": "OK",
  "Message": "Successfully Sent!",
  "Cost": "35",
  "MsgFollowUpUniqueCode": "ApiMSG.123456789"
}
```

If the request fails, `Status` is `"Failed"` and `Message` contains the error details.

### Example — Send One Message

```json
{
  "method": "SendSms",
  "userdata": {
    "username": "your_username",
    "password": "your_api_key"
  },
  "msgdata": [
    {
      "number": "256700111222",
      "message": "Hello World!",
      "senderid": "MyCompany",
      "priority": "0"
    }
  ]
}
```

### Example — Send Many Messages

```json
{
  "method": "SendSms",
  "userdata": {
    "username": "your_username",
    "password": "your_api_key"
  },
  "msgdata": [
    {
      "number": "256700111222",
      "message": "Hello World!",
      "senderid": "MyCompany",
      "priority": "0"
    },
    {
      "number": "256700111333",
      "message": "Hello World!",
      "senderid": "CustomID2",
      "priority": "0"
    },
    {
      "number": "256700111444",
      "message": "Hello other message!",
      "senderid": "MyCompany",
      "priority": "0"
    }
  ]
}
```

### Raw HTTP Example (Dart/Flutter)

```dart
var headers = {
  'Content-Type': 'application/json'
};
var request = http.Request('POST', Uri.parse('https://comms.egosms.co/api/v1/json/'));
request.body = json.encode({
  "method": "SendSms",
  "userdata": {
      "username": "api_username",
      "password": "api_key"
  },
  "msgdata": [
      {
        "number": "256700111222",
        "message": "Hello World!",
        "senderid": "MyCompany",
        "priority": "0"
      },
      {
        "number": "256700333444",
        "message": "Hello World 2!",
        "senderid": "MyCompany",
        "priority": "0"
      }
  ]
});
request.headers.addAll(headers);

http.StreamedResponse response = await request.send();

if (response.statusCode == 200) {
  print(await response.stream.bytesToString());
} else {
  print(response.reasonPhrase);
}
```

> Other raw HTTP examples are available in the original docs for Java, JS/TS, cURL, C# (RestSharp), Ruby, PHP (Guzzle), Go, and C (libcurl) — request them if needed.

### Using the SDK

**1. Install**

```bash
dart pub add comms_sdk
flutter pub add comms_sdk
```

**2. Authenticate**

```dart
import 'package:commssdk/commssdk.dart';

const apiUsername = "api_username";
const apiKey = "api_key";

// CommsSDK.useSandbox();
final sdk = await CommsSDK.authenticate(apiUsername, apiKey);
```

**3. Send**

```dart
final numbers = ["256789123456", "+256789123457", "256789123458"];
await sdk.sendSMS(numbers, "Sample SMS Message", senderID: "CustomSenderID", priority: MessagePriority.Highest);

final smsResponse = await sdk.querySendSMS(numbers, "Query send sms message");
print(smsResponse.status);
```

> **Note:** For query-based methods, the returned object is of type [`ApiResponse`](#apiresponse). SDKs are also available for Java, JS/TS, Rust, Kotlin, Go, Python, PHP, Ruby, and C# with equivalent methods (see [SDK Reference](#sdk-reference) for naming conventions per language).

---

## Checking Balance

### Request Structure

| Field | Type | Required | Description |
|---|---|---|---|
| `method` | string | Yes | Must be `"Balance"` |
| `userdata` | object | Yes | Authentication |
| `userdata.username` | string | Yes | Your API username |
| `userdata.password` | string | Yes | Your API password |

```json
{
  "method": "Balance",
  "userdata": {
    "username": "your_username",
    "password": "your_api_key"
  }
}
```

### Response Structure

```json
{
  "Status": "OK",
  "Balance": 2700
}
```

If the request fails, `Status` is `"Failed"` and `Message` contains the error details.

### Raw HTTP Example (Java / OkHttp)

```java
OkHttpClient client = new OkHttpClient().newBuilder()
    .build();
MediaType mediaType = MediaType.parse("application/json");
String json = """
{
   "method": "Balance",
   "userdata": {
      "username": "api_username",
      "password": "api_key"
   }
}
""";
RequestBody body = RequestBody.create(mediaType, json);
Request request = new Request.Builder()
    .url("https://comms.egosms.co/api/v1/json/")
    .method("POST", body)
    .addHeader("Content-Type", "application/json")
    .build();
Response response = client.newCall(request).execute();
```

> Other raw HTTP examples are available in the original docs for JS/TS, cURL, C# (RestSharp), Ruby, PHP (Guzzle), Dart/Flutter, Go, and C (libcurl) — request them if needed.

### Using the SDK

**1. Install**

```bash
dart pub add comms_sdk
flutter pub add comms_sdk
```

**2. Authenticate**

```dart
import 'package:commssdk/commssdk.dart';

const apiUsername = "api_username";
const apiKey = "api_key";

// CommsSDK.useSandbox();
final sdk = await CommsSDK.authenticate(apiUsername, apiKey);
```

**3. Query balance**

```dart
final balance = await sdk.getBalance();
print("Balance: $balance");

final balanceQuery = await sdk.queryBalance();
print("${balanceQuery.status} and balance: ${balanceQuery.balance}");
```

> **Note:** For query-based methods, the returned object is of type [`ApiResponse`](#apiresponse).

---

## Transaction Status Webhook

Used to receive delivery/transaction status updates. You provide an endpoint (e.g. `yoursite.com/transaction-status`), register it in your EgoSMS/Comms settings, and Comms will POST status updates to it as a JSON payload.

### Payload Structure

```json
{
  "MsgFollowUpUniqueCode": "ApiJsonSubmit64b8dd8aab0d05.55176607",
  "number": "+256777071434",
  "Status": "Success"
}
```

| Field | Type | Description |
|---|---|---|
| `MsgFollowUpUniqueCode` | string | Unique follow-up code for the submitted message |
| `number` | string | Recipient phone number |
| `Status` | string | Transaction status value for the message |

Persist the payload however you prefer (database, queue, log, etc.).

### Example Receiver (Java / Spring Boot)

```java
// DTO
public class TxStatusPayload {
    public String MsgFollowUpUniqueCode;
    public String number;
    public String Status;
}

@RestController
public class TxStatusController {

    @PostMapping("/transaction-status")
    public ResponseEntity<String> receive(@RequestBody TxStatusPayload payload) {
        // TODO: persist payload (DB, queue, etc.)
        System.out.println("Status: " + payload.Status + " for " + payload.number);
        return ResponseEntity.ok("OK");
    }
}
```

> Equivalent receiver examples are available in the original docs for Java (JAX-RS/Jersey), Rust (axum), Python (Flask), Go (net/http), Node.js (Express), and PHP (MySQL) — request them if needed.

---

## Error Handling

Every API response includes a `Status` field. If `Status` is `"Failed"`, check the `Message` field for details. Common causes: authentication failure, missing fields, or invalid parameters.

### Response Fields

| Field | Type | Description |
|---|---|---|
| `Status` | string | `"OK"` if the request succeeded, `"Failed"` otherwise |
| `Message` | string | Human-readable message with success/error details |
| `Cost` | string | Cost of sent messages (`SendSms` only) |
| `MsgFollowUpUniqueCode` | string | Unique tracking code for sent messages (`SendSms` only) |
| `Balance` | integer | Current account balance (`Balance` requests only) |

### Debug Checklist — When `Status` Is `"Failed"`

1. **Read `Message`** — it usually contains the exact reason (e.g. auth failure, missing field).
2. **Verify credentials** — confirm the API username and API key match your Comms settings.
3. **Validate the request payload** — make sure `method`, `userdata`, and all required fields are present.
4. **Check numbers and sender ID** — use international format for numbers; keep the sender ID ≤ 11 characters.
5. **Confirm environment** — use sandbox while testing, live for production.
6. **Retry with a minimal request** — send a single SMS with default settings to confirm the basics work.
7. **Capture the raw request/response** — log the outgoing JSON and the full response for support.

If the issue persists, contact support with the full response and your request payload.

---

## SDK Reference

The `CommsSDK` functions are consistent across all supported SDKs (Java, JS/TS, Rust, Kotlin, Go, Python, Dart/Flutter, PHP, Ruby, C#). Only naming style differs by language convention (e.g. `sendSMS` in Java/C# vs. `send_sms` in Python/Ruby).

### Authentication and Environment

| Function | Parameters | Returns | Notes |
|---|---|---|---|
| `authenticate` | `username`, `apiKey` | SDK instance | Creates and validates an SDK instance |
| `useSandBox` / `use_sandbox` | none | void | Switches to the sandbox API URL |
| `useLiveServer` / `use_live_server` | none | void | Switches to the live API URL |
| `withSenderId` / `with_sender_id` | `senderId` | SDK instance | Sets a default sender ID for outgoing messages |

### Send SMS

| Function | Parameters | Returns | Notes |
|---|---|---|---|
| `sendSMS` / `send_sms` | `number` or `numbers`, `message`, optional `senderId`, optional `priority` | boolean | Sends SMS, returns success/failure |
| `querySendSMS` / `query_send_sms` | `numbers`, `message`, `senderId`, `priority` | `ApiResponse` | Sends SMS, returns the full API response |

### Balance

| Function | Parameters | Returns | Notes |
|---|---|---|---|
| `getBalance` / `get_balance` | none | number | Returns the account balance |
| `queryBalance` / `query_balance` | none | `ApiResponse` | Returns the full API response with balance details |

---

## Model Reference

SDK response models that map to the raw API responses.

### `ApiResponseCode` (Enum)

Represents the status of an API call.

- `OK` — the request was successful
- `Failed` — the request failed

### `ApiResponse`

Returned by `sdk.querySendSMS()` and `sdk.queryBalance()`.

| Field | Type | Description |
|---|---|---|
| `status` | `ApiResponseCode` | Status of the request |
| `message` | String | Additional message from the API |
| `cost` | Integer | Cost of sending the message (`SendSms` only) |
| `messageFollowUpCode` | String | Unique code for tracking each successful request |
| `balance` | Double | User account balance (`Balance` requests only) |

### `MessagePriority` (Enum)

Sets SMS sending priority. **Lower value = higher priority.**

| Name | Value | Description |
|---|---|---|
| `HIGHEST` | 0 | Urgent |
| `HIGH` | 1 | Default, High |
| `MEDIUM` | 2 | Normal |
| `LOW` | 3 | Low |
| `LOWEST` | 4 | Defer / send later |

### `ApiRequest`

Represents an API request payload.

| Field | Type | Description |
|---|---|---|
| `method` | String | One of: `"SendSMS"` or `"Balance"` |
| `userdata` | `UserData` | The user's credentials (username & API key) |
| `messageData` | `List<MessageModel>` | Messages & recipients for the request |

### `MessageModel`

Describes a single message to be sent.

| Field | Type | Description |
|---|---|---|
| `number` | String | Recipient's phone number |
| `message` | String | Message content |
| `senderId` | String | Sender ID shown to the recipient |
| `priority` | `MessagePriority` | Priority of this message |

### `UserData`

Credentials used to authenticate API requests.

| Field | Type | Description |
|---|---|---|
| `username` | String | Your API username, found in Comms API settings |
| `apikey` | String | Your API key (also called "password"), found in Comms API settings |
