# 💊 Dawy — Surplus Medicine Donation & Pharmacy Inspection Platform

> **Dawy** is a full-stack **ASP.NET Core MVC (.NET 10)** web application designed to reduce pharmaceutical waste by connecting individuals who have valid surplus medicines with patients in need — safely mediated through a network of **verified partner pharmacies** and overseen by system administrators.

---

## 🎯 Project Overview & Problem Statement

Large quantities of valid, unused medicines are wasted in households every day, while many patients struggle to afford essential treatments. However, direct peer-to-peer medicine exchange poses serious health risks due to potential expiration, improper storage, or controlled substances.

**Dawy** solves this by introducing **verified partner pharmacies** as a trusted medical intermediary:
1. **Donors** submit surplus medicines/medical supplies with photos and select a verified partner pharmacy.
2. **Pharmacists** physically inspect each donation (checking expiry, seal integrity, and storage) and either approve it or reject it with a documented reason.
3. Only **approved, in-stock, non-expired** items appear in the public catalog for **Beneficiaries** to browse and request (with a mandatory prescription upload for medicines).
4. **Pharmacists** schedule pickup dates and optional handling fees, **Beneficiaries** complete payment, and **Pharmacists** confirm final delivery.
5. **Admins** verify partner pharmacies, manage users and roles, moderate donations, and monitor all sensitive actions via **Audit Logs**.

---

## 👥 User Roles & Permissions (RBAC)

| Role | Responsibilities & Permissions |
| :--- | :--- |
| **Donor (`Donor`)** | Submit new donations (medicines, vitamins, medical supplies/devices), track donation statuses, and cancel pending donations before inspection. |
| **Pharmacist (`Pharmacist`)** | Register and manage pharmacies, inspect incoming donations (approve with verified quantity or reject with reason), review beneficiary requests & prescriptions, schedule pickups, and confirm delivery. |
| **Beneficiary (`Beneficiary`)** | Browse, search, and filter the public catalog, request medicines with prescription uploads, track or cancel requests, and pay scheduled collection fees. |
| **Admin (`Admin`)** | View platform-wide KPIs, manage user accounts/roles/activation status, verify or revoke partner pharmacies, moderate donations, and review audit logs. |

---

## 🛡️ Security & Medical Safety Highlights

- **Medical Safety Guardrails:** Blocks cold-chain and controlled items (e.g., insulin, vaccines, narcotics), enforces a minimum 3-month remaining shelf life on donations, auto-rejects expired items during inspection, and requires prescription uploads for medicines.
- **Race-Condition Protection:** Stock deduction on requests runs inside a database transaction (`BeginTransactionAsync`) using atomic `ExecuteUpdateAsync` (`Quantity >= RequestedQuantity`), preventing negative stock under concurrent requests.
- **Authentication & Authorization:** `Cookie Authentication` with `IPasswordHasher<User>` (with automatic rehashing), unique `Email` and 14-digit `NationalId` constraints, strict `[Authorize(Roles = ...)]` enforcement, and `[ValidateAntiForgeryToken]` CSRF protection.
- **Accountability & Notifications:** Automatic internal `Notifications` across workflow transitions and `AuditLogs` recording administrative and inspection actions.

---

## 🛠️ Tech Stack

- **Backend:** ASP.NET Core MVC (`.NET 10.0`), C#
- **Database & ORM:** SQL Server, Entity Framework Core `10.0.14` (Code-First Migrations & Fluent API)
- **Authentication:** Cookie Authentication + Role-Based Access Control (RBAC)
- **Frontend:** Razor Views (`.cshtml`), Bootstrap 5, Bootstrap Icons, Cairo Arabic RTL UI

---

## 🗄️ Database Schema (6 Core Tables)

1. **`Users`** — Accounts, hashed passwords, roles, unique 14-digit `NationalId`, and `IsActive` status.
2. **`Pharmacies`** — Partner pharmacy profiles, verification flag (`IsVerified`), and pharmacist ownership.
3. **`Products`** — Donated medicines/supplies, quantity, `DateOnly` expiry date, status, image URL, and rejection reason.
4. **`Requests`** — Beneficiary requests, prescription image URL, requested quantity (`CHECK > 0`), status, scheduled `CollectionDate`, `FeeAmount`, `IsPaid`, and `CollectedAt`.
5. **`Notifications`** — Per-user notifications with read tracking (`IsRead`) and deep links.
6. **`AuditLogs`** — Immutable audit trail of user/admin actions, target entities, and timestamps.

---

## 📁 Project Structure

```text
Dawy/
├── Controllers/       # Account, Admin, Audit, Category, Collection, Donation, Home, Inspection, Notification, Payment, Pharmacy, Profile, Request
├── Data/              # DawiContext.cs & DbInitializer.cs
├── Migrations/        # EF Core Code-First Migrations
├── Models/            # Entities & Status/Role constants
├── ViewModels/        # Login, Register, Product, Request, EditProfile ViewModels
├── Views/             # Arabic RTL Razor Views organized by module
├── wwwroot/           # Static assets (CSS, JS, Images, Uploaded Products & Prescriptions)
├── Program.cs         # Service registration, middleware pipeline, and DB seeding
└── appsettings.json   # Database connection string configuration
```

---

## 🚀 Getting Started

1. **Clone the repository:**
   ```bash
   git clone https://github.com/RanaTarekAhmed/Dawi.git
   cd Dawi/Dawy
   ```
2. **Configure the database connection:**
   Open `appsettings.json` and set `ConnectionStrings:DefaultConnection` to match your local SQL Server instance.
3. **Run the application:**
   Open `Dawy.slnx` in Visual Studio and press `F5` (or run `dotnet run`).
   `DbInitializer` automatically applies pending EF Core migrations and seeds the following demo accounts:

| Role | Email | Password |
| :--- | :--- | :--- |
| **Admin** | `admin@dawi.com` | `Admin@123` |
| **Pharmacist** | `pharmacist.demo@dawi.com` | `Demo@123` |
| **Donor** | `donor.demo@dawi.com` | `Demo@123` |
| **Beneficiary** | `beneficiary.demo@dawi.com` | `Demo@123` |

---

## 🏢 Organization, Supervision & Team

- **Organization:** YAT
- **Instructor:** Eng. Eyad Ali

### Team Members
- Keroles
- Rana
- Abdelrahman
- Rahma
- Youssef
- Rodyna