import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { fileURLToPath, URL } from 'node:url'
import { copyFileSync } from 'node:fs'
import { resolve } from 'node:path'

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [
    vue(),
    // Plugin to copy PDF.js worker file
    {
      name: 'copy-pdfjs-worker',
      generateBundle() {
        // Copy the PDF.js worker file to the build output
        const workerSrc = resolve(__dirname, 'node_modules/pdfjs-dist/build/pdf.worker.min.js')
        const workerDest = resolve(__dirname, 'dist/pdf.worker.min.js')
        try {
          copyFileSync(workerSrc, workerDest)
        } catch (err) {
          console.warn('Could not copy PDF.js worker file:', err.message)
        }
      }
    }
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url))
    },
    // Ensure only one copy of PDF.js is bundled (prevents "Cannot read from private field" errors)
    dedupe: ['pdfjs-dist']
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
        secure: false
      }
    }
  },
  build: {
    target: 'es2015',
    assetsDir: '',
    rollupOptions: {
      input: {
        main: resolve(__dirname, 'index.html'),
        worker: resolve(__dirname, 'node_modules/pdfjs-dist/build/pdf.worker.min.js')
      },
      output: {
        entryFileNames: (chunkInfo) => {
          if (chunkInfo.name === 'worker') {
            return 'pdf.worker.min.js'
          }
          return 'assets/[name]-[hash].js'
        }
      }
    }
  },
  assetsInclude: ['**/*.worker.js']
})
