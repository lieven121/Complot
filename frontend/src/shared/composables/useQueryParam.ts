import { computed, type WritableComputedRef } from 'vue'
import { useRoute, useRouter } from 'vue-router'

interface Codec<T> {
  read: (value: string | undefined) => T
  write: (value: T) => string | undefined
}

const text: Codec<string | undefined> = { read: (v) => v, write: (v) => v }
/** "1" when on, absent when off. */
export const flag: Codec<boolean> = { read: (v) => v === '1', write: (v) => (v ? '1' : undefined) }

/** A filter kept in the URL query (?key=…) so a reload or shared link keeps it; `replace` avoids history spam. */
export function useQueryParam(key: string): WritableComputedRef<string | undefined>
export function useQueryParam<T>(key: string, codec: Codec<T>): WritableComputedRef<T>
export function useQueryParam<T>(key: string, codec: Codec<T> = text as unknown as Codec<T>) {
  const route = useRoute()
  const router = useRouter()
  return computed({
    get: () => codec.read(route.query[key] as string | undefined),
    set: (value: T) => router.replace({ query: { ...route.query, [key]: codec.write(value) } }),
  })
}
