# Diccionario de datos

> Fuente: Base de datos PostgreSQL `subasta` (esquema public). Tablas: 6.
>
> Documento generado automáticamente por `generase-documentation.yml` (Subasta.DocGen) el 2026-10-03 02:26 UTC. No editar manualmente.

## Resumen de tablas

| Tabla | Descripción | Columnas |
|---|---|---|
| [`auction_images`](#auction_images) | Imágenes de los artículos subastados | 7 |
| [`auctions`](#auctions) | Artículos publicados para subasta | 17 |
| [`bids`](#bids) | Ofertas (pujas) realizadas | 5 |
| [`categories`](#categories) | Categorías de artículos | 2 |
| [`notifications`](#notifications) | Notificaciones enviadas a los usuarios | 7 |
| [`users`](#users) | Usuarios registrados de la plataforma | 7 |

## auction_images

Imágenes de los artículos subastados

| # | Columna | Tipo de dato | Nulo | PK | FK | Valor por defecto | Descripción |
|---|---|---|---|---|---|---|---|
| 1 | `Id` | uuid | No | ✔ |  |  | Identificador de la imagen |
| 2 | `AuctionId` | uuid | No |  | → `auctions.Id` |  | Subasta a la que pertenece |
| 3 | `FileName` | character varying(200) | No |  |  |  | Nombre original del archivo |
| 4 | `ContentType` | character varying(50) | No |  |  |  | Tipo MIME de la imagen |
| 5 | `Data` | bytea | No |  |  |  | Contenido binario de la imagen |
| 6 | `SortOrder` | integer | No |  |  |  | Orden de visualización |
| 7 | `CreatedAt` | timestamp with time zone | No |  |  |  | Fecha de carga (UTC) |

**Llaves foráneas**

| Restricción | Columnas | Referencia | ON DELETE |
|---|---|---|---|
| `FK_auction_images_auctions_AuctionId` | AuctionId | `auctions`(Id) | CASCADE |

**Índices**

| Índice | Columnas | Único |
|---|---|---|
| `IX_auction_images_AuctionId` | AuctionId | No |

## auctions

Artículos publicados para subasta

| # | Columna | Tipo de dato | Nulo | PK | FK | Valor por defecto | Descripción |
|---|---|---|---|---|---|---|---|
| 1 | `Id` | uuid | No | ✔ |  |  | Identificador de la subasta |
| 2 | `Title` | character varying(120) | No |  |  |  | Título del artículo |
| 3 | `Description` | character varying(4000) | No |  |  |  | Descripción del artículo |
| 4 | `CategoryId` | integer | No |  | → `categories.Id` |  | Categoría del artículo |
| 5 | `StartingPrice` | numeric(18,2) | No |  |  |  | Precio inicial |
| 6 | `MinIncrement` | numeric(18,2) | No |  |  |  | Incremento mínimo entre pujas |
| 7 | `CurrentPrice` | numeric(18,2) | No |  |  |  | Precio actual (mejor puja) |
| 8 | `StartAt` | timestamp with time zone | No |  |  |  | Fecha/hora de inicio (UTC) |
| 9 | `EndAt` | timestamp with time zone | No |  |  |  | Fecha/hora de cierre (UTC) |
| 10 | `Status` | character varying(20) | No |  |  |  | Estado: Scheduled, Active, Finished, Cancelled |
| 11 | `SellerId` | uuid | No |  | → `users.Id` |  | Usuario que publica la subasta |
| 12 | `WinnerId` | uuid | Sí |  | → `users.Id` |  | Usuario adjudicatario |
| 13 | `WinningBidId` | uuid | Sí |  |  |  | Puja ganadora |
| 14 | `BidCount` | integer | No |  |  |  | Cantidad de pujas recibidas |
| 15 | `CreatedAt` | timestamp with time zone | No |  |  |  | Fecha de publicación (UTC) |
| 16 | `ClosedAt` | timestamp with time zone | Sí |  |  |  | Fecha de cierre/adjudicación (UTC) |
| 17 | `Version` | integer | No |  |  |  | Control de concurrencia optimista |

**Llaves foráneas**

| Restricción | Columnas | Referencia | ON DELETE |
|---|---|---|---|
| `FK_auctions_categories_CategoryId` | CategoryId | `categories`(Id) | RESTRICT |
| `FK_auctions_users_SellerId` | SellerId | `users`(Id) | RESTRICT |
| `FK_auctions_users_WinnerId` | WinnerId | `users`(Id) | RESTRICT |

**Índices**

| Índice | Columnas | Único |
|---|---|---|
| `IX_auctions_CategoryId` | CategoryId | No |
| `IX_auctions_SellerId` | SellerId | No |
| `IX_auctions_StartAt` | StartAt | No |
| `IX_auctions_Status_EndAt` | Status, EndAt | No |
| `IX_auctions_WinnerId` | WinnerId | No |

## bids

Ofertas (pujas) realizadas

| # | Columna | Tipo de dato | Nulo | PK | FK | Valor por defecto | Descripción |
|---|---|---|---|---|---|---|---|
| 1 | `Id` | uuid | No | ✔ |  |  | Identificador de la puja |
| 2 | `AuctionId` | uuid | No |  | → `auctions.Id` |  | Subasta pujada |
| 3 | `BidderId` | uuid | No |  | → `users.Id` |  | Usuario que puja |
| 4 | `Amount` | numeric(18,2) | No |  |  |  | Monto ofertado |
| 5 | `CreatedAt` | timestamp with time zone | No |  |  |  | Fecha/hora de la puja (UTC) |

**Llaves foráneas**

| Restricción | Columnas | Referencia | ON DELETE |
|---|---|---|---|
| `FK_bids_auctions_AuctionId` | AuctionId | `auctions`(Id) | CASCADE |
| `FK_bids_users_BidderId` | BidderId | `users`(Id) | RESTRICT |

**Índices**

| Índice | Columnas | Único |
|---|---|---|
| `IX_bids_AuctionId_Amount` | AuctionId, Amount | No |
| `IX_bids_BidderId` | BidderId | No |

## categories

Categorías de artículos

| # | Columna | Tipo de dato | Nulo | PK | FK | Valor por defecto | Descripción |
|---|---|---|---|---|---|---|---|
| 1 | `Id` | integer | No | ✔ |  |  | Identificador de la categoría |
| 2 | `Name` | character varying(60) | No |  |  |  | Nombre de la categoría |

**Índices**

| Índice | Columnas | Único |
|---|---|---|
| `IX_categories_Name` | Name | Sí |

## notifications

Notificaciones enviadas a los usuarios

| # | Columna | Tipo de dato | Nulo | PK | FK | Valor por defecto | Descripción |
|---|---|---|---|---|---|---|---|
| 1 | `Id` | uuid | No | ✔ |  |  | Identificador de la notificación |
| 2 | `UserId` | uuid | No |  | → `users.Id` |  | Usuario destinatario |
| 3 | `AuctionId` | uuid | Sí |  | → `auctions.Id` |  | Subasta relacionada |
| 4 | `Type` | character varying(30) | No |  |  |  | Tipo de notificación |
| 5 | `Message` | character varying(500) | No |  |  |  | Mensaje mostrado al usuario |
| 6 | `IsRead` | boolean | No |  |  |  | Indica si fue leída |
| 7 | `CreatedAt` | timestamp with time zone | No |  |  |  | Fecha de creación (UTC) |

**Llaves foráneas**

| Restricción | Columnas | Referencia | ON DELETE |
|---|---|---|---|
| `FK_notifications_auctions_AuctionId` | AuctionId | `auctions`(Id) | CASCADE |
| `FK_notifications_users_UserId` | UserId | `users`(Id) | CASCADE |

**Índices**

| Índice | Columnas | Único |
|---|---|---|
| `IX_notifications_AuctionId` | AuctionId | No |
| `IX_notifications_UserId_IsRead` | UserId, IsRead | No |

## users

Usuarios registrados de la plataforma

| # | Columna | Tipo de dato | Nulo | PK | FK | Valor por defecto | Descripción |
|---|---|---|---|---|---|---|---|
| 1 | `Id` | uuid | No | ✔ |  |  | Identificador único del usuario |
| 2 | `UserName` | character varying(50) | No |  |  |  | Nombre de usuario visible (único) |
| 3 | `Email` | character varying(150) | No |  |  |  | Correo electrónico (único) |
| 4 | `PasswordHash` | character varying(500) | No |  |  |  | Hash PBKDF2 de la contraseña |
| 5 | `Role` | character varying(20) | No |  |  |  | Rol: User o Admin |
| 6 | `IsActive` | boolean | No |  |  |  | Indica si la cuenta está habilitada |
| 7 | `CreatedAt` | timestamp with time zone | No |  |  |  | Fecha de registro (UTC) |

**Índices**

| Índice | Columnas | Único |
|---|---|---|
| `IX_users_Email` | Email | Sí |
| `IX_users_UserName` | UserName | Sí |
