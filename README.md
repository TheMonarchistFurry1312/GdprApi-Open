![GdprApi is a .NET 8 Web API designed for managing GDPR-compliant operations in a multi-tenant system](https://github.com/user-attachments/assets/0b4e40a2-a788-4e28-9b2b-f9b63d6fdf6c)

## Overview
GdprApi is a .NET 8 Web API designed for managing GDPR-compliant operations in a multi-tenant system. It provides endpoints for tenant creation, audit logging, and other functionalities, ensuring compliance with GDPR requirements such as accountability (Article 5(2)), data minimization (Article 5(1)(c)), and security of processing (Article 32). The API is built with ASP.NET Core, MongoDB for data storage, and Swagger for API documentation, making it suitable for developers building applications that require robust tenant management and auditability.

### Why it matters

The **General Data Protection Regulation (GDPR)** is a strict privacy law enforced by the European Union. Failing to comply can lead to fines of up to **€20 million** or **4% of your global annual revenue** — whichever is higher. And yes, it applies even if your app or business is based **outside of Europe**, as long as you handle **personal data of EU citizens**.

🔗 **Official resources**:

* 📘 [Full GDPR Regulation Text (EUR-Lex)](https://eur-lex.europa.eu/eli/reg/2016/679/oj)
* 📄 [GDPR Guidelines and FAQs (European Commission)](https://commission.europa.eu/law/law-topic/data-protection_en)

## ✅ GDPR Coverage

GdprApi has been designed to support critical GDPR requirements out of the box. The table below outlines the specific articles currently addressed by the API.

| Article | Title                                              | How it's addressed |
|---------|----------------------------------------------------|---------------------|
| **5(1)(c)** | Data Minimization                                 | Only necessary data is collected and stored with strict controls. |
| **5(2)**     | Accountability                                     | All operations are audited for traceability and compliance verification. |
| **6**        | Lawfulness of Processing                          | Explicit user consent is required and logged during registration. |
| **7**        | Conditions for Consent                            | Consent is granular, recorded, and revocable, ensuring legal validity. |
| **15**       | Right of Access                                   | Tenants can retrieve their personal data securely on request. |
| **20**       | Right to Data Portability                         | Users can download their data in JSON or CSV formats. |
| **25**       | Data Protection by Design and by Default          | Data is pseudonymized using SHA-256 and encrypted in a separate mapping. |
| **30**       | Records of Processing Activities                  | A detailed audit trail is kept for all tenant-related operations. |
| **32**       | Security of Processing                            | Data is encrypted, access is restricted, and actions are validated via tenant/client ID. |

> ℹ️ This list will grow as new features are implemented to support broader GDPR coverage.

## Features
- **Multi-Tenant Support**: Segregates data by tenant using a `TenantId` field, ensuring isolation between tenants.
- **GDPR Compliance**: Includes comprehensive audit logging for tracking actions like tenant creation, consent management, and data subject rights requests (e.g., data erasure, portability).
- **Secure Authentication**: Uses JWT-based authentication with tenant-specific access control policies.
- **Audit Logging**: Logs all significant actions (e.g., create, update, delete) with details such as actor, timestamp, and success status, stored in MongoDB.
- **Swagger Documentation**: Provides interactive API documentation with XML comments for clear endpoint descriptions.
- **Rate Limiting**: Prevents abuse by limiting requests to 100 per minute per client.
- **CORS**: Restricts access to trusted origins, enhancing security for cross-origin requests.

## Prerequisites
- **.NET 8 SDK**: Install from [https://dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0).
- **Docker**: A running Docker instance is required to run MongoDB locally. Download from [https://www.docker.com/products/docker-desktop/](https://www.docker.com/products/docker-desktop/).
- **Visual Studio 2022** or **VS Code** with C# extensions for development.
- **Postman** or **Swagger UI** for testing API endpoints.

## Setup Instructions
1. **Clone the Repository**:
   ``` git clone https://github.com/HeyBaldur/GdprApi.git ```

### Running the API Locally
This section explains how to run the API using Docker for MongoDB and Visual Studio 2022.

---

### Step 1: Run MongoDB with Docker
To run the API, you need a local MongoDB instance. Using Docker is the recommended approach for a clean, consistent environment.

1.  **Start the MongoDB Container**: Open your terminal or command prompt and run the following command. This will download and start a MongoDB container named `GdprMongoDb` on port `27017`.

    ```bash
    docker run --name GdprMongoDb -d -p 27017:27017 mongo
    ```

    - `--name GdprMongoDb`: Assigns a readable name to the container.
    - `-d`: Runs the container in detached mode (in the background).
    - `-p 27017:27017`: Maps port `27017` on your local machine to port `27017` inside the container.
    - `mongo`: Specifies the official MongoDB image to use.

2.  **Verify the Container is Running**: To ensure the container is up and running, use the following command:

    ```bash
    docker ps
    ```

    You should see `GdprMongoDb` listed in the output.

3.  **Stop and Clean Up**: When you're finished, you can stop and remove the container to free up resources:

    ```bash
    # Stop the container
    docker compose down --volumes

    # Remove the container
    docker compose up --build
    ```
    
---

### Step 2: Configure and Run in Visual Studio 2022
1.  **Open the Project**: Open the `GdprApi.sln` solution file in **Visual Studio 2022**.

2.  **Restore Dependencies**: Visual Studio should automatically restore the required .NET dependencies. If not, right-click the solution in the Solution Explorer and select **Restore NuGet Packages**.

3.  **Check Configuration**: Verify that your `appsettings.json` file has the correct MongoDB connection string. It should point to the Docker container you just started. Each of these variables must be set as environment variables. Whenever you add a new environment variable, it's recommended to reset the computer or environment for it to take effect.

    ```json
    {
     "Logging": {
       "LogLevel": {
         "Default": "Information",
         "Microsoft.AspNetCore": "Warning"
       }
     },
     "MongoDbSettings": {
       "ConnectionString": "${MONGO_DB_CNX}",
       "DatabaseName": "${MONGO_DB_NAME}"
     },
     "AppSettings": {
       "Token": "${APP_SETTINGS_TOKEN}",
       "AccessTokenExpirationMinutes": "${ACCESS_TOKEN_EXPIRATION_MINUTES}",
       "RefreshTokenExpirationMinutes": "${REFRESH_TOKEN_EXPIRATION_MINUTES}"
     },
     "LicenseKey": "${LICENSE_KEY}"
    }
    ```

4.  **Run the API**:
    -   Press **F5** or click the **Start Debugging** button in Visual Studio.
    -   The application will build and launch a browser window, taking you to the Swagger UI at `https://localhost:<port>/swagger`.

5.  **Test Endpoints**: From the Swagger UI, you can interact with the API endpoints to create tenants, view audit logs, and more. For example, use the `Tenant` endpoints to test the multi-tenant functionality.
6.  **API documentation** is available in https://github.com/HeyBaldur/GdprApi-Open/blob/master/Docs.md

## 🛡️ License

This project is licensed under the **BSD 3-Clause License**.

You are free to:

- Use the software for personal or commercial purposes  
- Modify and adapt it to your needs  
- Redistribute it, with or without modifications  

**Requirements:**

- You must retain the original copyright notice  
- You may not use the name of the author or contributors to endorse or promote derived products without prior permission  

