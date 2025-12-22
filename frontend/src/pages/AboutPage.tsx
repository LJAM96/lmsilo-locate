import { MapPin, Github, ExternalLink } from 'lucide-react';

export function AboutPage() {
    return (
        <div className="p-6 lg:p-8 max-w-2xl">
            <div className="mb-8">
                <h1 className="font-serif text-2xl lg:text-3xl text-surface-800 dark:text-cream-100">
                    About GeoLens
                </h1>
                <p className="text-surface-500 dark:text-surface-400 mt-1">
                    AI-powered image geolocation
                </p>
            </div>

            <div className="space-y-6">
                {/* Logo & Version */}
                <div className="card flex items-center gap-4">
                    <div className="w-16 h-16 rounded-2xl bg-olive-600 flex items-center justify-center">
                        <MapPin className="w-8 h-8 text-white" />
                    </div>
                    <div>
                        <h2 className="font-serif text-xl text-surface-800 dark:text-cream-100">
                            GeoLens
                        </h2>
                        <p className="text-surface-500 dark:text-surface-400">
                            Version 5.0.0 (React)
                        </p>
                    </div>
                </div>

                {/* Description */}
                <div className="card space-y-4">
                    <h3 className="font-semibold text-surface-800 dark:text-cream-100">
                        About
                    </h3>
                    <p className="text-surface-600 dark:text-cream-200">
                        GeoLens uses the GeoCLIP deep learning model to predict the geographic
                        location where a photograph was taken, based solely on visual content.
                    </p>
                    <p className="text-surface-600 dark:text-cream-200">
                        The model analyzes features like landmarks, vegetation, architecture,
                        and terrain to estimate the most likely location. Results are presented
                        as ranked predictions with confidence scores.
                    </p>
                </div>

                {/* Technology */}
                <div className="card space-y-4">
                    <h3 className="font-semibold text-surface-800 dark:text-cream-100">
                        Technology
                    </h3>
                    <div className="grid grid-cols-2 gap-4 text-sm">
                        <div>
                            <p className="text-surface-500 dark:text-surface-400">AI Model</p>
                            <p className="text-surface-700 dark:text-cream-200">GeoCLIP</p>
                        </div>
                        <div>
                            <p className="text-surface-500 dark:text-surface-400">Backend</p>
                            <p className="text-surface-700 dark:text-cream-200">FastAPI + Python</p>
                        </div>
                        <div>
                            <p className="text-surface-500 dark:text-surface-400">Frontend</p>
                            <p className="text-surface-700 dark:text-cream-200">React + TypeScript</p>
                        </div>
                        <div>
                            <p className="text-surface-500 dark:text-surface-400">Styling</p>
                            <p className="text-surface-700 dark:text-cream-200">Tailwind CSS</p>
                        </div>
                    </div>
                </div>

                {/* Links */}
                <div className="card space-y-4">
                    <h3 className="font-semibold text-surface-800 dark:text-cream-100">
                        Links
                    </h3>
                    <div className="space-y-2">
                        <a
                            href="https://github.com/VicenteVivan/geo-clip"
                            target="_blank"
                            rel="noopener noreferrer"
                            className="flex items-center gap-2 text-olive-600 dark:text-olive-400 hover:underline"
                        >
                            <Github className="w-4 h-4" />
                            GeoCLIP on GitHub
                            <ExternalLink className="w-3 h-3" />
                        </a>
                        <a
                            href="https://huggingface.co/geolocal/StreetCLIP"
                            target="_blank"
                            rel="noopener noreferrer"
                            className="flex items-center gap-2 text-olive-600 dark:text-olive-400 hover:underline"
                        >
                            <ExternalLink className="w-4 h-4" />
                            Model on HuggingFace
                            <ExternalLink className="w-3 h-3" />
                        </a>
                    </div>
                </div>

                {/* Credits */}
                <div className="card space-y-4">
                    <h3 className="font-semibold text-surface-800 dark:text-cream-100">
                        Credits
                    </h3>
                    <p className="text-sm text-surface-600 dark:text-cream-200">
                        This application is built upon the GeoCLIP research by Vicente Vivanco Cepeda,
                        Gaurav Kumar, and Mubarak Shah.
                    </p>
                    <p className="text-xs text-surface-500 dark:text-surface-400 font-mono">
                        © 2024 GeoLens. Licensed under MIT.
                    </p>
                </div>
            </div>
        </div>
    );
}
