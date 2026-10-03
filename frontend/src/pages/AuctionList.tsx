import { useEffect, useState, type FormEvent } from 'react';
import { useSearchParams } from 'react-router-dom';
import { api } from '../api/client';
import type { AuctionFilters, AuctionStatus, AuctionSummary, Category } from '../api/types';
import { AuctionCard, EmptyState, ErrorMessage, Spinner } from '../components/Common';

const TABS: { key: AuctionStatus; label: string }[] = [
  { key: 'active', label: 'Activas' },
  { key: 'upcoming', label: 'Próximas' },
  { key: 'finished', label: 'Finalizadas' },
];

const PAGE_SIZE = 12;

export function AuctionList() {
  const [params, setParams] = useSearchParams();
  const status = (params.get('status') as AuctionStatus | null) ?? 'active';
  const page = Number(params.get('page') ?? '1');

  const [categories, setCategories] = useState<Category[]>([]);
  const [items, setItems] = useState<AuctionSummary[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [search, setSearch] = useState(params.get('search') ?? '');
  const [categoryId, setCategoryId] = useState(params.get('categoryId') ?? '');
  const [minPrice, setMinPrice] = useState(params.get('minPrice') ?? '');
  const [maxPrice, setMaxPrice] = useState(params.get('maxPrice') ?? '');
  const sort = (params.get('sort') as AuctionFilters['sort'] | null) ?? 'endingSoon';

  useEffect(() => {
    api.categories().then(setCategories).catch(() => setCategories([]));
  }, []);

  useEffect(() => {
    const filters: AuctionFilters = {
      status,
      search: params.get('search') ?? undefined,
      categoryId: params.get('categoryId') ? Number(params.get('categoryId')) : undefined,
      minPrice: params.get('minPrice') ? Number(params.get('minPrice')) : undefined,
      maxPrice: params.get('maxPrice') ? Number(params.get('maxPrice')) : undefined,
      sort,
      page,
      pageSize: PAGE_SIZE,
    };
    setLoading(true);
    setError(null);
    api
      .auctions(filters)
      .then((r) => {
        setItems(r.items);
        setTotal(r.totalCount);
      })
      .catch((e: Error) => setError(e.message))
      .finally(() => setLoading(false));
  }, [params, status, page, sort]);

  const update = (changes: Record<string, string>) => {
    const next = new URLSearchParams(params);
    Object.entries(changes).forEach(([k, v]) => (v ? next.set(k, v) : next.delete(k)));
    if (!('page' in changes)) next.delete('page');
    setParams(next);
  };

  const applyFilters = (e: FormEvent) => {
    e.preventDefault();
    if (minPrice && maxPrice && Number(minPrice) > Number(maxPrice)) {
      setError('El precio mínimo no puede ser mayor al máximo.');
      return;
    }
    update({ search: search.trim().slice(0, 100), categoryId, minPrice, maxPrice });
  };

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  return (
    <>
      <section className="hero">
        <h1>Subastas en línea, en tiempo real</h1>
        <p>Publique sus artículos, oferte al instante y reciba notificaciones de cada movimiento.</p>
      </section>

      <div className="tabs" role="tablist">
        {TABS.map((t) => (
          <button
            key={t.key}
            type="button"
            role="tab"
            aria-selected={status === t.key}
            className={status === t.key ? 'tab active' : 'tab'}
            onClick={() => update({ status: t.key })}
          >
            {t.label}
          </button>
        ))}
      </div>

      <form className="filters card" onSubmit={applyFilters}>
        <input
          type="search"
          placeholder="Buscar artículos…"
          value={search}
          maxLength={100}
          onChange={(e) => setSearch(e.target.value)}
          aria-label="Buscar"
        />
        <select value={categoryId} onChange={(e) => setCategoryId(e.target.value)} aria-label="Categoría">
          <option value="">Todas las categorías</option>
          {categories.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
        <input
          type="number"
          min="0"
          step="0.01"
          placeholder="Precio mín."
          value={minPrice}
          onChange={(e) => setMinPrice(e.target.value)}
          aria-label="Precio mínimo"
        />
        <input
          type="number"
          min="0"
          step="0.01"
          placeholder="Precio máx."
          value={maxPrice}
          onChange={(e) => setMaxPrice(e.target.value)}
          aria-label="Precio máximo"
        />
        <select value={sort} onChange={(e) => update({ sort: e.target.value })} aria-label="Ordenar">
          <option value="endingSoon">Cierran pronto</option>
          <option value="newest">Más recientes</option>
          <option value="priceAsc">Precio: menor a mayor</option>
          <option value="priceDesc">Precio: mayor a menor</option>
        </select>
        <button type="submit" className="btn btn-primary">
          Filtrar
        </button>
      </form>

      <ErrorMessage message={error} />
      {loading && <Spinner />}
      {!loading && items.length === 0 && <EmptyState>No hay subastas que coincidan con los filtros.</EmptyState>}
      {!loading && items.length > 0 && (
        <div className="grid">
          {items.map((a) => (
            <AuctionCard key={a.id} auction={a} />
          ))}
        </div>
      )}

      {totalPages > 1 && (
        <div className="pagination">
          <button type="button" className="btn btn-ghost" disabled={page <= 1} onClick={() => update({ page: String(page - 1) })}>
            ← Anterior
          </button>
          <span>
            Página {page} de {totalPages}
          </span>
          <button
            type="button"
            className="btn btn-ghost"
            disabled={page >= totalPages}
            onClick={() => update({ page: String(page + 1) })}
          >
            Siguiente →
          </button>
        </div>
      )}
    </>
  );
}
