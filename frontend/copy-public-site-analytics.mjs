import { copyFile, mkdir } from 'node:fs/promises'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const outputDirectory = process.argv[2]

if (!outputDirectory) {
  throw new Error('Pass the static output directory to copy-public-site-analytics.mjs.')
}

const scriptDirectory = dirname(fileURLToPath(import.meta.url))
const source = resolve(scriptDirectory, 'public-site-analytics/public-site-analytics.js')
const destination = resolve(process.cwd(), outputDirectory, 'public-site-analytics.js')

await mkdir(dirname(destination), { recursive: true })
await copyFile(source, destination)
