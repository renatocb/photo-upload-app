import React, { useState } from "react";
import axios from "axios";
import "./App.css";

interface Photo {
  url: string;
  name: string;
}

function App() {
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState<boolean>(false);
  const [message, setMessage] = useState<string>("");
  const [userPhotos, setUserPhotos] = useState<string[]>([]);

  const API_BASE_URL =
    process.env.REACT_APP_API_URL || "https://localhost:7003";

  const handleFileSelect = (event: React.ChangeEvent<HTMLInputElement>) => {
    if (event.target.files && event.target.files[0]) {
      setSelectedFile(event.target.files[0]);
      setMessage("");
    }
  };

  const handleUpload = async () => {
    if (!selectedFile) {
      setMessage("Por favor, selecione um arquivo.");
      return;
    }

    setUploading(true);
    const formData = new FormData();
    formData.append("file", selectedFile);

    try {
      const response = await axios.post(
        `${API_BASE_URL}/api/photos/upload`,
        formData,
        {
          headers: {
            "Content-Type": "multipart/form-data",
          },
        }
      );

      setMessage(
        "Upload realizado com sucesso! A imagem está sendo processada."
      );
      setSelectedFile(null);

      // Limpar o input de arquivo
      const fileInput = document.getElementById(
        "file-input"
      ) as HTMLInputElement;
      if (fileInput) fileInput.value = "";
    } catch (error: any) {
      setMessage(
        "Erro ao fazer upload: " + (error.response?.data || error.message)
      );
    } finally {
      setUploading(false);
    }
  };

  const fetchMyPhotos = async () => {
    try {
      const response = await axios.get(`${API_BASE_URL}/api/photos/my-photos`);
      setUserPhotos(response.data);
    } catch (error: any) {
      setMessage("Erro ao carregar fotos: " + error.message);
    }
  };

  return (
    <div className="App">
      <header className="App-header">
        <h1>📸 Photo Upload</h1>
        <p>Faça upload de suas fotos favoritas</p>
      </header>

      <main className="main-content">
        <div className="upload-section">
          <input
            id="file-input"
            type="file"
            accept="image/*"
            onChange={handleFileSelect}
            disabled={uploading}
          />
          <button
            onClick={handleUpload}
            disabled={uploading || !selectedFile}
            className="upload-button"
          >
            {uploading ? "Enviando..." : "📤 Fazer Upload"}
          </button>
        </div>

        {message && (
          <div
            className={`message ${
              message.includes("Erro") ? "error" : "success"
            }`}
          >
            {message}
          </div>
        )}

        <div className="photos-section">
          <button onClick={fetchMyPhotos} className="load-button">
            📷 Carregar Minhas Fotos
          </button>

          <div className="photos-grid">
            {userPhotos.map((photoUrl, index) => (
              <div key={index} className="photo-item">
                <img src={photoUrl} alt={`Upload ${index}`} />
              </div>
            ))}
          </div>
        </div>
      </main>
    </div>
  );
}

export default App;
