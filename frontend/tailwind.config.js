/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        paper: "#F4F6F5",
        sheet: "#FFFFFF",
        ink: "#14232E",
        muted: "#5B6B72",
        grid: "#D3DCDB",
        river: "#1F6F8B",
        deep: "#0B3A53",
        ochre: "#A8742A",
        rust: "#A3412F",
      },
      fontFamily: {
        sans: ["Archivo", "system-ui", "sans-serif"],
      },
    },
  },
  plugins: [],
};
