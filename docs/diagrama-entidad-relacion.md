# Diagrama entidad-relación

> Fuente: Modelo EF Core (Npgsql).
>
> Documento generado automáticamente por `generase-documentation.yml` (Subasta.DocGen) el 2026-10-03 02:25 UTC. No editar manualmente.

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
        character_varying_50 ContentType "Tipo MIME de la imagen"
        timestamp_with_time_zone CreatedAt "Fecha de carga (UTC)"
        bytea Data "Contenido binario de la imagen"
        character_varying_200 FileName "Nombre original del archivo"
        integer SortOrder "Orden de visualización"
    }
    auctions {
        uuid Id PK "Identificador de la subasta"
        integer BidCount "Cantidad de pujas recibidas"
        integer CategoryId FK "Categoría del artículo"
        timestamp_with_time_zone ClosedAt "Fecha de cierre/adjudicación (UTC)"
        timestamp_with_time_zone CreatedAt "Fecha de publicación (UTC)"
        numeric_18_2 CurrentPrice "Precio actual (mejor puja)"
        character_varying_4000 Description "Descripción del artículo"
        timestamp_with_time_zone EndAt "Fecha/hora de cierre (UTC)"
        numeric_18_2 MinIncrement "Incremento mínimo entre pujas"
        uuid SellerId FK "Usuario que publica la subasta"
        timestamp_with_time_zone StartAt "Fecha/hora de inicio (UTC)"
        numeric_18_2 StartingPrice "Precio inicial"
        character_varying_20 Status "Estado: Scheduled, Active, Finished, Cancelled"
        character_varying_120 Title "Título del artículo"
        integer Version "Control de concurrencia optimista"
        uuid WinnerId FK "Usuario adjudicatario"
        uuid WinningBidId "Puja ganadora"
    }
    bids {
        uuid Id PK "Identificador de la puja"
        numeric_18_2 Amount "Monto ofertado"
        uuid AuctionId FK "Subasta pujada"
        uuid BidderId FK "Usuario que puja"
        timestamp_with_time_zone CreatedAt "Fecha/hora de la puja (UTC)"
    }
    categories {
        integer Id PK "Identificador de la categoría"
        character_varying_60 Name UK "Nombre de la categoría"
    }
    notifications {
        uuid Id PK "Identificador de la notificación"
        uuid AuctionId FK "Subasta relacionada"
        timestamp_with_time_zone CreatedAt "Fecha de creación (UTC)"
        boolean IsRead "Indica si fue leída"
        character_varying_500 Message "Mensaje mostrado al usuario"
        character_varying_30 Type "Tipo de notificación"
        uuid UserId FK "Usuario destinatario"
    }
    users {
        uuid Id PK "Identificador único del usuario"
        timestamp_with_time_zone CreatedAt "Fecha de registro (UTC)"
        character_varying_150 Email UK "Correo electrónico (único)"
        boolean IsActive "Indica si la cuenta está habilitada"
        character_varying_500 PasswordHash "Hash PBKDF2 de la contraseña"
        character_varying_20 Role "Rol: User o Admin"
        character_varying_50 UserName UK "Nombre de usuario visible (único)"
    }
```
