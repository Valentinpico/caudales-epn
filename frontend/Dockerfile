# Dev: Vite dev server with HMR. Source is bind-mounted by compose.
FROM node:22-alpine

WORKDIR /app

COPY package*.json ./
RUN npm install

COPY . ./

EXPOSE 5173
CMD ["npm", "run", "dev"]
