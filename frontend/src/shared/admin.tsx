import { createContext, useContext, useState } from "react";
import type { ReactNode } from "react";
import { api, setAdminPassword } from "./api";

interface AdminCtx {
  isAdmin: boolean;
  login: (password: string) => Promise<boolean>;
  logout: () => void;
}

const Ctx = createContext<AdminCtx | null>(null);

export function AdminProvider({ children }: { children: ReactNode }) {
  const [isAdmin, setIsAdmin] = useState(false);
  const login = async (password: string) => {
    const ok = await api.verifyAdmin(password);
    if (ok) {
      setAdminPassword(password);
      setIsAdmin(true);
    }
    return ok;
  };
  const logout = () => {
    setAdminPassword(null);
    setIsAdmin(false);
  };
  return <Ctx.Provider value={{ isAdmin, login, logout }}>{children}</Ctx.Provider>;
}

export function useAdmin() {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error("useAdmin must be used inside AdminProvider");
  return ctx;
}
