// Fails when an installed package is not under a common open-source license.
// Guards against upgrades that silently switch to a commercial license (as PrimeVue 5 did).
import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'

const allowed = new Set([
  'MIT',
  'ISC',
  'Apache-2.0',
  'BSD-2-Clause',
  'BSD-3-Clause',
  '0BSD',
  'BlueOak-1.0.0',
  'CC0-1.0',
  'CC-BY-4.0',
  'Unlicense',
  'Python-2.0',
  'MPL-2.0',
])
const isAllowed = (license) =>
  typeof license === 'string' &&
  license
    .replace(/[()]/g, '')
    .split(/ OR | AND /)
    .every((part) => allowed.has(part.trim()))

const store = 'node_modules/.pnpm'
const offenders = new Map()
for (const entry of readdirSync(store)) {
  const modules = join(store, entry, 'node_modules')
  if (!existsSync(modules)) continue
  for (const name of readdirSync(modules)) {
    const dirs = name.startsWith('@') ? readdirSync(join(modules, name)).map((sub) => join(name, sub)) : [name]
    for (const dir of dirs) {
      const file = join(modules, dir, 'package.json')
      if (!existsSync(file)) continue
      const pkg = JSON.parse(readFileSync(file, 'utf8'))
      if (!isAllowed(pkg.license)) offenders.set(`${pkg.name}@${pkg.version}`, JSON.stringify(pkg.license ?? pkg.licenses ?? null))
    }
  }
}

if (offenders.size) {
  console.error('Packages without an allowed open-source license:')
  for (const [name, license] of offenders) console.error(`  ${name}: ${license}`)
  process.exit(1)
}
console.log('All installed packages use an allowed open-source license.')
