# Diagrama entidad-relación

> Fuente: Base de datos PostgreSQL `subasta` (esquema public).
>
> Documento generado automáticamente por `generase-documentation.yml` (Subasta.DocGen) el 2026-10-03 02:26 UTC. No editar manualmente.

```mermaid
erDiagram
    auctions ||--o{ auction_images : "AuctionId"
    categories ||--o{ auctions : "CategoryId"
    users ||--o{ auctions : "SellerId"
    users |o--o{ auctions : "WinnerId"
    auctions ||--o{ bids : "AuctionId"
    users ||--o{ bids : "BidderId"
    auctions |o--o{ notifications : "AuctionId"
    users ||--o{ notifications : "UserId"
    auction_images {
        uuid Id PK "Identificador de la imagen"
        uuid AuctionId FK "Subasta a la que pertenece"
        character_varying_200 FileName "Nombre original del archivo"
        character_varying_50 ContentType "Tipo MIME de la imagen"
        bytea Data "Contenido binario de la imagen"
        integer SortOrder "Orden de visualización"
        timestamp_with_time_zone CreatedAt "Fecha de carga (UTC)"
    }
    auctions {
        uuid Id PK "Identificador de la subasta"
        character_varying_120 Title "Título del artículo"
        character_varying_4000 Description "Descripción del artículo"
        integer CategoryId FK "Categoría del artículo"
        numeric_18_2 StartingPrice "Precio inicial"
        numeric_18_2 MinIncrement "Incremento mínimo entre pujas"
        numeric_18_2 CurrentPrice "Precio actual (mejor puja)"
        timestamp_with_time_zone StartAt "Fecha/hora de inicio (UTC)"
        timestamp_with_time_zone EndAt "Fecha/hora de cierre (UTC)"
        character_varying_20 Status "Estado: Scheduled, Active, Finished, Cancelled"
        uuid SellerId FK "Usuario que publica la subasta"
        uuid WinnerId FK "Usuario adjudicatario"
        uuid WinningBidId "Puja ganadora"
        integer BidCount "Cantidad de pujas recibidas"
        timestamp_with_time_zone CreatedAt "Fecha de publicación (UTC)"
        timestamp_with_time_zone ClosedAt "Fecha de cierre/adjudicación (UTC)"
        integer Version "Control de concurrencia optimista"
    }
    bids {
        uuid Id PK "Identificador de la puja"
        uuid AuctionId FK "Subasta pujada"
        uuid BidderId FK "Usuario que puja"
        numeric_18_2 Amount "Monto ofertado"
        timestamp_with_time_zone CreatedAt "Fecha/hora de la puja (UTC)"
    }
    categories {
        integer Id PK "Identificador de la categoría"
        character_varying_60 Name UK "Nombre de la categoría"
    }
    notifications {
        uuid Id PK "Identificador de la notificación"
        uuid UserId FK "Usuario destinatario"
        uuid AuctionId FK "Subasta relacionada"
        character_varying_30 Type "Tipo de notificación"
        character_varying_500 Message "Mensaje mostrado al usuario"
        boolean IsRead "Indica si fue leída"
        timestamp_with_time_zone CreatedAt "Fecha de creación (UTC)"
    }
    users {
        uuid Id PK "Identificador único del usuario"
        character_varying_50 UserName UK "Nombre de usuario visible (único)"
        character_varying_150 Email UK "Correo electrónico (único)"
        character_varying_500 PasswordHash "Hash PBKDF2 de la contraseña"
        character_varying_20 Role "Rol: User o Admin"
        boolean IsActive "Indica si la cuenta está habilitada"
        timestamp_with_time_zone CreatedAt "Fecha de registro (UTC)"
    }
```
