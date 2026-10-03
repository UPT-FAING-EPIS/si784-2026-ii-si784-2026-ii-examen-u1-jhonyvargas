# Diagrama de componentes

> Componentes del frontend (React), de la API (.NET) y de la persistencia, con sus dependencias.
>
> Documento generado automáticamente por `generase-documentation.yml` (Subasta.DocGen) el 2026-10-03 02:32 UTC. No editar manualmente.

```mermaid
flowchart LR
    user((Usuario / Administrador))
    subgraph FE["Frontend SPA - React + Vite"]
        direction TB
        page_Admin["Página: Admin"]
        page_AuctionDetail["Página: AuctionDetail"]
        page_AuctionList["Página: AuctionList"]
        page_AuthPages["Página: AuthPages"]
        page_CreateAuction["Página: CreateAuction"]
        page_Dashboard["Página: Dashboard"]
        page_Notifications["Página: Notifications"]
        comp_Common["Componente: Common"]
        comp_Layout["Componente: Layout"]
        ctx_AuthContext["Contexto: AuthContext"]
        ctx_NotificationContext["Contexto: NotificationContext"]
        apiClient[["api/client.ts - Cliente REST"]]
        rtClient[["api/realtime.ts - Cliente SignalR"]]
    end
    subgraph BE["Backend API - ASP.NET Core (contenedor)"]
        direction TB
        AdminController["AdminController<br/>/admin"]
        AuctionsController["AuctionsController<br/>/auctions"]
        AuthController["AuthController<br/>/auth"]
        BidsController["BidsController<br/>/bids"]
        UserController["UserController<br/>/user"]
        AuctionHub{{"AuctionHub - WebSocket /hubs/auctions"}}
        IAdminService(["IAdminService"])
        IAuctionLifecycleService(["IAuctionLifecycleService"])
        IAuctionService(["IAuctionService"])
        IAuthService(["IAuthService"])
        IBidService(["IBidService"])
        INotificationService(["INotificationService"])
        AuctionClosingBackgroundService[/"AuctionClosingBackgroundService - proceso en segundo plano"/]
        AppDbContext[("AppDbContext - EF Core")]
    end
    db[("PostgreSQL")]
    user --> FE
    page_Admin --> apiClient
    page_AuctionDetail --> apiClient
    page_AuctionList --> apiClient
    page_AuthPages --> apiClient
    page_CreateAuction --> apiClient
    page_Dashboard --> apiClient
    page_Notifications --> apiClient
    ctx_NotificationContext --> rtClient
    page_AuctionDetail --> rtClient
    apiClient -- "HTTPS REST/JSON + JWT" --> BE
    rtClient -- "WSS SignalR" --> AuctionHub
    AdminController --> IAdminService
    AdminController --> IAuctionService
    AdminController --> IAuctionLifecycleService
    AuctionsController --> IAuctionService
    AuthController --> IAuthService
    BidsController --> IBidService
    UserController --> IAuctionService
    UserController --> IBidService
    UserController --> INotificationService
    IAdminService --> AppDbContext
    IAuctionLifecycleService --> AppDbContext
    IAuctionLifecycleService --> INotificationService
    IAuctionLifecycleService -. "push" .-> AuctionHub
    IAuctionService --> AppDbContext
    IAuctionService --> INotificationService
    IAuctionService -. "push" .-> AuctionHub
    IAuthService --> AppDbContext
    IBidService --> AppDbContext
    IBidService --> INotificationService
    IBidService -. "push" .-> AuctionHub
    INotificationService --> AppDbContext
    INotificationService -. "push" .-> AuctionHub
    AuctionClosingBackgroundService --> IAuctionLifecycleService
    AppDbContext -- "Npgsql / TLS" --> db
```

## Endpoints expuestos

| Método | Ruta | Controlador | Acción | Autorización |
|---|---|---|---|---|
| GET | `/admin/stats` | AdminController | Stats | JWT (Admin) |
| GET | `/admin/users` | AdminController | Users | JWT (Admin) |
| PATCH | `/admin/users/{id:guid}` | AdminController | UpdateUser | JWT (Admin) |
| GET | `/admin/auctions` | AdminController | Auctions | JWT (Admin) |
| POST | `/admin/auctions/{id:guid}/close` | AdminController | Close | JWT (Admin) |
| POST | `/admin/auctions/{id:guid}/cancel` | AdminController | Cancel | JWT (Admin) |
| POST | `/auctions` | AuctionsController | Create | JWT |
| GET | `/auctions` | AuctionsController | List | Pública |
| GET | `/auctions/{id:guid}` | AuctionsController | GetById | Pública |
| POST | `/auctions/{id:guid}/images` | AuctionsController | UploadImage | JWT |
| GET | `/auctions/{id:guid}/images/{imageId:guid}` | AuctionsController | GetImage | Pública |
| POST | `/auctions/{id:guid}/cancel` | AuctionsController | Cancel | JWT |
| GET | `/categories` | AuctionsController | Categories | Pública |
| POST | `/auth/register` | AuthController | Register | Pública |
| POST | `/auth/login` | AuthController | Login | Pública |
| GET | `/auth/me` | AuthController | Me | JWT |
| POST | `/bids` | BidsController | Place | JWT |
| GET | `/bids` | BidsController | ByAuction | Pública |
| GET | `/user/auctions` | UserController | Auctions | JWT |
| GET | `/user/bids` | UserController | Bids | JWT |
| GET | `/user/notifications` | UserController | Notifications | JWT |
| GET | `/user/notifications/unread-count` | UserController | UnreadCount | JWT |
| POST | `/user/notifications/{id:guid}/read` | UserController | MarkRead | JWT |
| POST | `/user/notifications/read-all` | UserController | MarkAllRead | JWT |
| WS | `/hubs/auctions` | AuctionHub | JoinAuction / LeaveAuction | Opcional (JWT) |

**Eventos en tiempo real (servidor → cliente):** `BidPlaced`, `AuctionClosed`, `AuctionStarted`, `AuctionCancelled`, `Notification`.
