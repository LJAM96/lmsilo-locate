"""
Model Manager with Idle Timeout Unloading for Locate

Manages GeoCLIP model with automatic unloading after idle period.
"""

import os
import time
import threading
import logging
import gc
from typing import Optional, Any

logger = logging.getLogger(__name__)

DEFAULT_IDLE_TIMEOUT = int(os.getenv("MODEL_IDLE_TIMEOUT", "600"))


class GeoClipModelManager:
    """Thread-safe GeoCLIP model manager with idle timeout."""
    
    def __init__(self, idle_timeout: int = DEFAULT_IDLE_TIMEOUT):
        self._predictor: Optional[Any] = None
        self._last_used: float = 0
        self._timeout = idle_timeout
        self._lock = threading.RLock()
        self._running = True
        self._device = os.getenv("DEVICE", "auto")
        
        self._start_cleanup_thread()
        logger.info(f"GeoClipModelManager initialized with {idle_timeout}s idle timeout")
    
    def _start_cleanup_thread(self):
        def cleanup_loop():
            while self._running:
                time.sleep(60)
                self._check_timeout()
        
        thread = threading.Thread(target=cleanup_loop, daemon=True)
        thread.start()
    
    def _check_timeout(self):
        with self._lock:
            if self._predictor is not None and self._last_used > 0:
                if time.time() - self._last_used > self._timeout:
                    logger.info("GeoCLIP model idle, unloading to save memory")
                    self._unload()
    
    def _unload(self):
        self._predictor = None
        self._last_used = 0
        gc.collect()
        
        try:
            import torch
            if torch.cuda.is_available():
                torch.cuda.empty_cache()
        except ImportError:
            pass
        
        logger.info("GeoCLIP model unloaded")
    
    def get_predictor(self):
        """Get the GeoCLIP predictor, loading if necessary."""
        with self._lock:
            self._last_used = time.time()
            
            if self._predictor is None:
                from ..llocale import GeoClipPredictor
                logger.info(f"Loading GeoCLIP predictor (device={self._device})")
                start = time.time()
                self._predictor = GeoClipPredictor(device=self._device)
                logger.info(f"GeoCLIP loaded in {time.time() - start:.1f}s")
            
            return self._predictor
    
    @property
    def is_loaded(self) -> bool:
        return self._predictor is not None
    
    def shutdown(self):
        self._running = False
        with self._lock:
            if self._predictor:
                self._unload()


# Global singleton
_manager: Optional[GeoClipModelManager] = None


def get_model_manager() -> GeoClipModelManager:
    global _manager
    if _manager is None:
        _manager = GeoClipModelManager()
    return _manager


def get_predictor():
    """Convenience function to get the GeoCLIP predictor."""
    return get_model_manager().get_predictor()
