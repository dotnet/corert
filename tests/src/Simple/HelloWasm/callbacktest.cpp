extern "C" {
    void test_callback(void(*f)(void))
    {
        f();
    }
}
