# GDPR API Documentation

This API enables GDPR-compliant operations within a multi-tenant system, including tenant registration, authentication, audience data management, and secure data handling in accordance with GDPR Articles 6, 7, 15, 20, 25, 30, and 32.

> **Security Requirements**: Most endpoints (except authentication and tenant registration) require:
>
> * A valid JWT token with a matching `tenantId` claim
> * A `ClientId` header specifying the authorized client

---

## 📌 Endpoints Overview

| Method | Endpoint                                      | Summary                                              |                                                     |
| ------ | --------------------------------------------- | ---------------------------------------------------- | --------------------------------------------------- |
| POST   | `/api/auth`                                   | Register a new tenant (anonymous access)             |                                                     |
| POST   | `/api/auth/authenticate`                      | Authenticate and receive JWT + refresh token         |                                                     |
| POST   | `/api/auth/{tenantId}/refresh-token`          | Refresh JWT access token using a valid refresh token |                                                     |
| PUT    | `/api/tenant/{tenantId}`                      | Update tenant metadata                               |                                                     |
| GET    | `/api/tenant/{tenantId}`                      | Retrieve tenant data (original, unpseudonymized)     |                                                     |
| GET    | `/api/tenant/{tenantId}/download?format=JSON` | CSV                                                  | Download tenant data for portability (GDPR Art. 20) |
| POST   | `/api/audience/{tenantId}/audience`           | Save audience data (nested, arbitrary structure)     |                                                     |
| GET    | `/api/audience/{tenantId}/audience`           | Retrieve tenant audience data                        |                                                     |

---

## 🔐 Authentication & Authorization

### 🔑 `POST /api/auth`

Registers a new tenant. Requires:

* `email`, `fullName`, `password`, `tenantName`, `userName`
* Captures consent (GDPR Art. 7)
* Logs registration attempt in the audit log

> No `ClientId` or token required

**Sample JSON:**

```json
{
  "email": "hello@example.com",
  "password": "hello@example.com",
  "confirmPassword": "hello@example.com",
  "tenantName": "Acme SaaS",
  "userName": "acme_admin",
  "fullName": "Alice Doe",
  "websiteUrl": "https://acme.io",
  "consentAccepted": true
}
```

### 🔑 `POST /api/auth/authenticate`

Authenticates a tenant and returns:

* JWT access token
* Refresh token

Logs all attempts. Required body:

* `email`, `password`

> No `ClientId` required

**Sample JSON:**

```json
{
  "email": "hello@example.com",
  "password": "hello@example.com"
}
```

### ↺ `POST /api/auth/{tenantId}/refresh-token`

Uses a valid refresh token to issue a new access token.

* Requires `Authorization: Bearer <JWT>`
* Requires `ClientId` header

Logs refresh token attempts for audit purposes.

**Sample JSON:**

```json
{
  "token": "eyJhbGciOi..."
}
```

---

## 🏢 Tenant Operations

### 🛠 `PUT /api/tenant/{tenantId}`

Updates tenant data. Fields:

* `fullName`, `userName`, `websiteUrl`

Logs update operations.

* Requires `Authorization: Bearer <JWT>`
* Requires `ClientId` header

**Sample JSON:**

```json
{
  "fullName": "Alice Doe",
  "userName": "alice_admin",
  "websiteUrl": "https://acme.io"
}
```

### 📄 `GET /api/tenant/{tenantId}`

Retrieves original (unpseudonymized) tenant data.

* Complies with GDPR Art. 15 (right of access)
* Requires `Authorization: Bearer <JWT>`
* Requires `ClientId` header

### 📅 `GET /api/tenant/{tenantId}/download`

Downloads all tenant data in CSV or JSON.

* Complies with GDPR Art. 20 (data portability)
* Requires `Authorization: Bearer <JWT>`
* Requires `ClientId` header

---

## 👥 Audience Data

### ➕ `POST /api/audience/{tenantId}/audience`

Saves arbitrary audience data (e.g. profile data, behaviors) in a flexible structure.

* Complies with GDPR Art. 6 (lawful basis)
* Requires `Authorization: Bearer <JWT>`
* Requires `ClientId` header

**Sample JSON:**

```json
{
  "tenantId": "123456789",
  "details": {
    "email": "user@example.com",
    "preferences": {
      "newsletter": true,
      "notifications": false
    },
    "signupDate": "2025-08-06T12:00:00Z"
  }
}
```

### 🔍 `GET /api/audience/{tenantId}/audience`

Retrieves all audience records tied to a specific tenant.

* Complies with GDPR Art. 15 (right of access)
* Requires `Authorization: Bearer <JWT>`
* Requires `ClientId` header

---

## 🧱 Security & Compliance Highlights

* 🔒 **Pseudonymization**: Email and full names are hashed via SHA-256
* 🗃️ **Encrypted Mappings**: Original values stored in `PseudonymMappings` collection
* 📜 **Audit Logs**: All operations are logged for traceability (GDPR Art. 30)
* ✅ **Explicit Consent**: Captured during registration (GDPR Art. 7)
* 📉 **Data Minimization**: Only required data is stored or processed
* 🧪 **Strict Authorization**: Tenant ID and Client ID are enforced in all secured endpoints

---

## 📝 Example Headers

```http
Authorization: Bearer eyJhbGciOi...
ClientId: my-app-client-id
Content-Type: application/json
```
