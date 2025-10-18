import React from "react";
import { render, screen } from "@testing-library/react";
import App from "./App";

test("renderiza o título do app", () => {
  render(<App />);
  expect(screen.getByText(/photo upload/i)).toBeInTheDocument();
});

test("renderiza botão de upload", () => {
  render(<App />);
  expect(screen.getByText(/fazer upload/i)).toBeInTheDocument();
});
