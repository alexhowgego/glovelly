export function mapFrameSize(width: number, height: number): string | null {
  if (width <= 0 || height <= 0) return null
  const scale = Math.min(1, 640 / width, 640 / height)
  return `${Math.max(32, Math.floor(width * scale))}x${Math.max(32, Math.floor(height * scale))}`
}
