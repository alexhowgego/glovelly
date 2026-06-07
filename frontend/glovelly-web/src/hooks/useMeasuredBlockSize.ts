import { useEffect, useRef, useState } from 'react'

export function useMeasuredBlockSize<T extends HTMLElement>() {
  const ref = useRef<T | null>(null)
  const [blockSize, setBlockSize] = useState(0)

  useEffect(() => {
    const element = ref.current
    if (!element) {
      return
    }

    const updateBlockSize = () => {
      setBlockSize(Math.ceil(element.getBoundingClientRect().height))
    }

    updateBlockSize()

    const observer = new ResizeObserver(updateBlockSize)
    observer.observe(element)

    return () => observer.disconnect()
  }, [])

  return { ref, blockSize }
}
