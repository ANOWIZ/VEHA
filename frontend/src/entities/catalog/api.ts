import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/shared/api/client";
import type { Page } from "@/shared/api/types";
import type { LicensingModel } from "@/entities/quote/model";

export interface Vendor {
  id: string;
  name: string;
  country: string | null;
}

export interface Product {
  id: string;
  vendor_id: string;
  name: string;
  edition: string | null;
  licensing_model: LicensingModel;
  price_currency: string;
  is_russian_registry: boolean;
}

export interface PriceItem {
  id: string;
  product_id: string;
  metric: string;
  price: string;
  currency: string;
  partner_discount_level: string | null;
  valid_from: string;
  valid_to: string | null;
}

export interface ProductDetail extends Product {
  vendor: Vendor | null;
  price_items: PriceItem[];
}

export function useVendors() {
  return useQuery({
    queryKey: ["vendors"],
    queryFn: () => api.get<Vendor[]>("/catalog/vendors"),
  });
}

export function useCreateVendor() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: { name: string; country?: string }) =>
      api.post<Vendor>("/catalog/vendors", input),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["vendors"] }),
  });
}

export function useProducts(q?: string, vendorId?: string) {
  return useQuery({
    queryKey: ["products", q ?? "", vendorId ?? ""],
    queryFn: () =>
      api.get<Page<Product>>("/catalog/products", {
        q,
        vendor_id: vendorId,
        limit: 200,
      }),
  });
}

export function useProduct(productId: string | undefined) {
  return useQuery({
    queryKey: ["product", productId],
    queryFn: () => api.get<ProductDetail>(`/catalog/products/${productId}`),
    enabled: !!productId,
  });
}

export function useCreateProduct() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: {
      vendor_id: string;
      name: string;
      edition?: string;
      licensing_model: LicensingModel;
      price_currency?: string;
      is_russian_registry?: boolean;
    }) => api.post<Product>("/catalog/products", input),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["products"] }),
  });
}

export function useAddPrice(productId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: {
      metric: string;
      price: string;
      currency?: string;
      valid_from: string;
      valid_to?: string | null;
    }) => api.post<PriceItem>(`/catalog/products/${productId}/prices`, input),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["product", productId] }),
  });
}
